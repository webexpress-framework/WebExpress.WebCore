using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// Builds the shared store and the transport from the <c>WebExpress:Cluster</c> settings and
    /// hands them to every subsystem. Without the block it falls back to an in-process store and
    /// no transport, which is the exact behavior of a single instance.
    /// </summary>
    public sealed class ClusterManager : IClusterManager, ISystemComponent
    {
        private const string LockScope = "lock";

        /// <summary>
        /// The clock difference to another instance from which on it is reported. Stored
        /// deadlines and the acceptance window of messages tolerate far more, but a difference of
        /// seconds already means the clocks are not synchronized and will drift further.
        /// </summary>
        internal static readonly TimeSpan ClockSkewWarning = TimeSpan.FromSeconds(5);

        private readonly ConcurrentDictionary<string, TimeSpan> _clockSkew = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _skewReported = new(StringComparer.Ordinal);

        private readonly IHttpServerContext _httpServerContext;
        private readonly ConcurrentDictionary<string, List<Action<ClusterMessage>>> _subscribers = new(StringComparer.Ordinal);
        private readonly object _gate = new();
        private IClusterStore _store;
        private IClusterTransport _transport;

        /// <summary>
        /// Returns the id of this instance within the cluster.
        /// </summary>
        public string NodeId { get; private set; }

        /// <summary>
        /// Determines whether other instances share the state or receive the messages.
        /// </summary>
        public bool IsClustered => Store.IsShared || Transport is not null;

        /// <summary>
        /// Returns the store holding the state every instance must see.
        /// </summary>
        public IClusterStore Store
        {
            get
            {
                lock (_gate)
                {
                    return _store;
                }
            }
        }

        /// <summary>
        /// Returns how far the clock of every instance that sent a message ran ahead of this one.
        /// </summary>
        public IReadOnlyDictionary<string, TimeSpan> ClockSkew => _clockSkew;

        /// <summary>
        /// Returns the transport forwarding messages, or null for a single instance.
        /// </summary>
        public IClusterTransport Transport
        {
            get
            {
                lock (_gate)
                {
                    return _transport;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The server context supplying the cluster settings.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ClusterManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _httpServerContext = httpServerContext;

            Configure(httpServerContext?.Configuration?
                .GetSection(ConfigurationPath.Combine(HttpServerSettings.Section, nameof(HttpServerSettings.Cluster)))
                .Get<ClusterSettings>());
        }

        /// <summary>
        /// Builds the node id, the store and the transport from the settings. Kept apart from the
        /// constructor, because the component activator calls the constructor with whatever it can
        /// inject and the settings are not among it.
        /// </summary>
        /// <param name="settings">The cluster settings, or null for a single instance.</param>
        internal void Configure(ClusterSettings settings)
        {
            NodeId = ResolveNodeId(settings?.NodeId, Environment.GetEnvironmentVariable);

            UseStore(string.IsNullOrWhiteSpace(settings?.StatePath)
                ? new MemoryClusterStore()
                : new FileClusterStore(Path.GetFullPath(settings.StatePath, Environment.CurrentDirectory)));

            UseTransport(settings?.Peers?.Any(x => !string.IsNullOrWhiteSpace(x)) == true
                ? new HttpClusterTransport(NodeId, DecodeSecret(settings.Secret), settings.Peers, _httpServerContext?.Log)
                : null);

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:clustermanager.initialization", NodeId, IsClustered)
            );
        }

        /// <summary>
        /// Replaces the store.
        /// </summary>
        /// <param name="store">The new store.</param>
        public void UseStore(IClusterStore store)
        {
            ArgumentNullException.ThrowIfNull(store);

            lock (_gate)
            {
                _store = store;
            }
        }

        /// <summary>
        /// Replaces the transport.
        /// </summary>
        /// <param name="transport">The new transport, or null to stop forwarding messages.</param>
        public void UseTransport(IClusterTransport transport)
        {
            IClusterTransport previous;

            lock (_gate)
            {
                previous = _transport;

                if (ReferenceEquals(previous, transport))
                {
                    return;
                }

                if (previous is not null)
                {
                    previous.Received -= OnReceived;
                }

                _transport = null;
            }

            previous?.Dispose();

            if (transport is not null)
            {
                AttachTransport(transport);
            }
        }

        /// <summary>
        /// Sends a message to every other instance.
        /// </summary>
        /// <param name="topic">The topic subscribers listen on.</param>
        /// <param name="payload">The content.</param>
        /// <param name="cancellationToken">Cancels the delivery.</param>
        /// <returns>A task that completes once every instance was tried.</returns>
        public async Task PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default)
        {
            var transport = Transport;

            if (transport is null)
            {
                return;
            }

            try
            {
                await transport.SendAsync(topic, payload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // the publishing request already served its own clients; the others miss one live update
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Listens for messages other instances publish on a topic.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <param name="handler">Receives each message.</param>
        /// <returns>A handle that stops the subscription when disposed.</returns>
        public IDisposable Subscribe(string topic, Action<ClusterMessage> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topic);
            ArgumentNullException.ThrowIfNull(handler);

            var list = _subscribers.GetOrAdd(topic, _ => []);

            lock (list)
            {
                list.Add(handler);
            }

            return new Subscription(() =>
            {
                lock (list)
                {
                    list.Remove(handler);
                }
            });
        }

        /// <summary>
        /// Takes a cluster-wide lock built on the atomic add of the store.
        /// </summary>
        /// <param name="name">The name of the lock.</param>
        /// <param name="lifetime">How long the lock holds when its owner dies without releasing it.</param>
        /// <param name="timeout">How long to wait for the lock.</param>
        /// <returns>A handle that releases the lock when disposed, or null when the timeout passed.</returns>
        public IDisposable Lock(string name, TimeSpan lifetime, TimeSpan timeout)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var token = Encoding.UTF8.GetBytes(NodeId + "/" + Guid.NewGuid().ToString("N"));
            var deadline = DateTime.UtcNow + timeout;
            var delay = 20;

            while (true)
            {
                var store = Store;

                if (store.TryAdd(LockScope, name, token, lifetime))
                {
                    return new Subscription(() =>
                    {
                        // only the owner releases; a lock that expired and was taken over stays with its new owner
                        if (store.Get(LockScope, name)?.AsSpan().SequenceEqual(token) == true)
                        {
                            store.Remove(LockScope, name);
                        }
                    });
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return null;
                }

                Thread.Sleep(delay);
                delay = Math.Min(delay * 2, 500);
            }
        }

        /// <summary>
        /// Releases the transport and the store.
        /// </summary>
        public void Dispose()
        {
            UseTransport(null);

            (Store as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Determines the node id: the configured one, else the host name container runtimes set
        /// per instance, else the machine name.
        /// </summary>
        /// <param name="configured">The configured node id.</param>
        /// <param name="environmentVariable">Reads an environment variable.</param>
        /// <returns>The node id.</returns>
        internal static string ResolveNodeId(string configured, Func<string, string> environmentVariable)
        {
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim();
            }

            var host = environmentVariable("HOSTNAME");

            return string.IsNullOrWhiteSpace(host) ? Environment.MachineName : host.Trim();
        }

        /// <summary>
        /// Decodes the shared secret, refusing one too weak to authenticate messages on a public listener.
        /// </summary>
        /// <param name="secret">The Base64 encoded secret.</param>
        /// <returns>The secret bytes.</returns>
        private static byte[] DecodeSecret(string secret)
        {
            byte[] bytes;

            try
            {
                bytes = Convert.FromBase64String(secret ?? string.Empty);
            }
            catch (FormatException)
            {
                bytes = [];
            }

            if (bytes.Length < 32)
            {
                throw new InvalidOperationException("WebExpress:Cluster:Secret must hold at least 256 random bits, Base64 encoded, when peers are configured.");
            }

            return bytes;
        }

        /// <summary>
        /// Starts dispatching what a transport receives.
        /// </summary>
        /// <param name="transport">The transport.</param>
        private void AttachTransport(IClusterTransport transport)
        {
            lock (_gate)
            {
                _transport = transport;
                transport.Received += OnReceived;
            }
        }

        /// <summary>
        /// Hands a received message to the subscribers of its topic.
        /// </summary>
        /// <param name="sender">The transport.</param>
        /// <param name="message">The message.</param>
        private void OnReceived(object sender, ClusterMessage message)
        {
            if (message is null)
            {
                return;
            }

            MeasureClock(message);

            if (!_subscribers.TryGetValue(message.Topic, out var list))
            {
                return;
            }

            Action<ClusterMessage>[] handlers;

            lock (list)
            {
                handlers = [.. list];
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    // one broken subscriber must not starve the others
                    _httpServerContext?.Log?.Exception(ex);
                }
            }
        }

        /// <summary>
        /// Records how far the clock of the sending instance differs from this one and reports a
        /// difference that means the clocks are not synchronized, at most every ten minutes.
        /// </summary>
        /// <param name="message">The received message.</param>
        internal void MeasureClock(ClusterMessage message)
        {
            if (message.Sent is not { } sent || string.IsNullOrEmpty(message.Origin))
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var skew = sent - now;

            _clockSkew[message.Origin] = skew;

            if (skew.Duration() < ClockSkewWarning || now - _skewReported.GetValueOrDefault(message.Origin) < TimeSpan.FromMinutes(10))
            {
                return;
            }

            _skewReported[message.Origin] = now;
            _httpServerContext?.Log?.Warning
            (
                I18N.Translate("webexpress.webcore:clustermanager.clockskew", message.Origin, skew.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
            );
        }

        /// <summary>
        /// Runs an action once when disposed.
        /// </summary>
        /// <param name="release">The action.</param>
        private sealed class Subscription(Action release) : IDisposable
        {
            private Action _release = release;

            /// <summary>
            /// Runs the action, the first time only.
            /// </summary>
            public void Dispose()
            {
                Interlocked.Exchange(ref _release, null)?.Invoke();
            }
        }
    }
}
