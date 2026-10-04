using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebLog;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// Forwards messages to the other instances over plain http, so a cluster needs no broker.
    /// Peers are listed explicitly or found through dns, which is how Kubernetes headless
    /// services and Docker Compose service names expose every instance of a deployment.
    /// </summary>
    /// <remarks>
    /// The receiving endpoint is part of the public listener, so every message is signed with
    /// the shared cluster secret and carries its own time and id: a forged message fails the
    /// signature, and a captured one is refused once it is older than the replay window or was
    /// already seen.
    /// </remarks>
    public sealed class HttpClusterTransport : IClusterTransport
    {
        /// <summary>
        /// The path the receiving endpoint listens on, outside every application route.
        /// </summary>
        public const string Path = "/_cluster/bus";

        /// <summary>
        /// The header carrying the signature of the message body.
        /// </summary>
        public const string SignatureHeader = "X-WebExpress-Cluster-Signature";

        /// <summary>
        /// How far the time of a message may lie from the receiver's clock. It bounds how long a
        /// captured message could be replayed and must absorb the clock skew between hosts.
        /// </summary>
        internal static readonly TimeSpan ReplayWindow = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The largest message accepted. Live messages are small; the limit keeps a peer that
        /// knows the secret but misbehaves from exhausting memory.
        /// </summary>
        internal const int MaxMessageBytes = 1024 * 1024;

        // dns answers are reused for a short while, so a burst of messages does not resolve the
        // name each time while scaled instances still show up within seconds
        private static readonly TimeSpan ResolveInterval = TimeSpan.FromSeconds(10);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly string _nodeId;
        private readonly byte[] _secret;
        private readonly IReadOnlyList<string> _peers;
        private readonly HttpClient _client;
        private readonly ILog _log;
        private readonly TimeProvider _clock;
        private readonly Func<string, Task<IPAddress[]>> _resolve;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _skewReported = new(StringComparer.Ordinal);
        private readonly HashSet<IPAddress> _localAddresses;
        private readonly SemaphoreSlim _resolveGate = new(1, 1);
        private IReadOnlyList<Uri> _resolved = [];
        private DateTimeOffset _resolvedAt = DateTimeOffset.MinValue;

        /// <summary>
        /// Raised for every authentic message another instance published.
        /// </summary>
        public event EventHandler<ClusterMessage> Received;

        /// <summary>
        /// The wire format of a message. Time and id are inside the signed body, so neither can
        /// be changed without breaking the signature.
        /// </summary>
        /// <param name="Topic">The topic.</param>
        /// <param name="Origin">The publishing node.</param>
        /// <param name="Time">The publishing time in unix milliseconds.</param>
        /// <param name="Id">The unique id, used to refuse replays.</param>
        /// <param name="Payload">The content.</param>
        private sealed record Envelope
        (
            [property: JsonPropertyName("topic")] string Topic,
            [property: JsonPropertyName("origin")] string Origin,
            [property: JsonPropertyName("time")] long Time,
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("payload")] byte[] Payload
        );

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodeId">The node id of this instance.</param>
        /// <param name="secret">The shared secret; at least 256 bits.</param>
        /// <param name="peers">The peers, as base uris or <c>dns://name:port</c> entries.</param>
        /// <param name="log">The log receiving delivery failures.</param>
        /// <param name="handler">The http handler, replaceable for tests.</param>
        /// <param name="timeProvider">The clock, replaceable for tests.</param>
        /// <param name="resolve">The dns lookup, replaceable for tests.</param>
        public HttpClusterTransport
        (
            string nodeId,
            byte[] secret,
            IEnumerable<string> peers,
            ILog log = null,
            HttpMessageHandler handler = null,
            TimeProvider timeProvider = null,
            Func<string, Task<IPAddress[]>> resolve = null
        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
            ArgumentNullException.ThrowIfNull(secret);

            if (secret.Length < 32)
            {
                throw new ArgumentException("The cluster secret requires at least 256 random bits.", nameof(secret));
            }

            _nodeId = nodeId;
            _secret = secret;
            _peers = (peers ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            _log = log;
            _clock = timeProvider ?? TimeProvider.System;
            _resolve = resolve ?? Dns.GetHostAddressesAsync;
            _client = new HttpClient(handler ?? new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            _localAddresses = handler is null ? LocalAddresses() : [];

            foreach (var peer in _peers)
            {
                ParsePeer(peer, out _, out _);
            }
        }

        /// <summary>
        /// Sends a message to every peer in parallel. A peer that is down or slow only loses this
        /// message; it must never hold up the request that published it.
        /// </summary>
        /// <param name="topic">The topic receivers dispatch on.</param>
        /// <param name="payload">The content.</param>
        /// <param name="cancellationToken">Cancels the delivery.</param>
        /// <returns>A task that completes once every peer was tried.</returns>
        public async Task SendAsync(string topic, byte[] payload, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topic);
            ArgumentNullException.ThrowIfNull(payload);

            var envelope = new Envelope(topic, _nodeId, _clock.GetUtcNow().ToUnixTimeMilliseconds(), Guid.NewGuid().ToString("N"), payload);
            var body = JsonSerializer.SerializeToUtf8Bytes(envelope, _jsonOptions);
            var signature = SignBase64(body);
            var peers = await ResolvePeersAsync().ConfigureAwait(false);

            await Task.WhenAll(peers.Select(peer => PostAsync(peer, body, signature, cancellationToken))).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies and dispatches a message another instance posted.
        /// </summary>
        /// <param name="body">The raw request body.</param>
        /// <param name="signature">The value of the signature header.</param>
        /// <returns>True when the message was authentic and fresh; false when it was refused.</returns>
        internal bool Receive(byte[] body, string signature)
        {
            if (body is null || body.Length == 0 || body.Length > MaxMessageBytes || string.IsNullOrEmpty(signature))
            {
                return false;
            }

            byte[] given;
            try
            {
                given = Convert.FromBase64String(signature);
            }
            catch (FormatException)
            {
                return false;
            }

            if (!CryptographicOperations.FixedTimeEquals(given, Sign(body)))
            {
                return false;
            }

            Envelope envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<Envelope>(body, _jsonOptions);
            }
            catch (JsonException)
            {
                return false;
            }

            if (envelope is null || string.IsNullOrEmpty(envelope.Topic) || string.IsNullOrEmpty(envelope.Id) || envelope.Payload is null)
            {
                return false;
            }

            var now = _clock.GetUtcNow();
            var sent = DateTimeOffset.FromUnixTimeMilliseconds(envelope.Time);

            if ((now - sent).Duration() > ReplayWindow)
            {
                // an authentic message this old is almost always a clock that went astray, which
                // would otherwise fail silently; reported at most every ten minutes per instance
                var last = _skewReported.GetValueOrDefault(envelope.Origin ?? string.Empty);

                if (now - last > TimeSpan.FromMinutes(10))
                {
                    _skewReported[envelope.Origin ?? string.Empty] = now;
                    _log?.Warning($"Cluster messages from '{envelope.Origin}' are refused: its clock differs from this instance's by {(sent - now).TotalSeconds:0.0} seconds, beyond the accepted {ReplayWindow.TotalSeconds:0} seconds.");
                }

                return false;
            }

            PruneSeen(now);

            // remembered for twice the window, since a message may arrive up to one window early or late
            if (!_seen.TryAdd(envelope.Id, now + ReplayWindow + ReplayWindow))
            {
                return false;
            }

            // a dns entry that lists this instance makes it post to itself; the echo is dropped
            if (string.Equals(envelope.Origin, _nodeId, StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                Received?.Invoke(this, new ClusterMessage(envelope.Topic, envelope.Origin, envelope.Payload, sent));
            }
            catch (Exception ex)
            {
                // a failing subscriber must not make the sender think the peer is broken
                _log?.Exception(ex);
            }

            return true;
        }

        /// <summary>
        /// Releases the http client.
        /// </summary>
        public void Dispose()
        {
            _client.Dispose();
            _resolveGate.Dispose();
        }

        /// <summary>
        /// Posts one message to one peer.
        /// </summary>
        /// <param name="peer">The base uri of the peer.</param>
        /// <param name="body">The signed body.</param>
        /// <param name="signature">The signature of the body.</param>
        /// <param name="cancellationToken">Cancels the delivery.</param>
        /// <returns>A task that completes once the peer answered or failed.</returns>
        private async Task PostAsync(Uri peer, byte[] body, string signature, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(peer, Path))
                {
                    Content = new ByteArrayContent(body)
                };
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.TryAddWithoutValidation(SignatureHeader, signature);

                using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _log?.Warning($"Cluster peer '{peer}' refused a message with status {(int)response.StatusCode}.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // a peer that is starting, stopping or gone; the message is live only and is dropped
                _log?.Debug($"Cluster peer '{peer}' is unreachable: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the base uris of the peers, resolving dns entries at most every few seconds.
        /// </summary>
        /// <returns>The peers to deliver to.</returns>
        private async Task<IReadOnlyList<Uri>> ResolvePeersAsync()
        {
            if (_clock.GetUtcNow() - _resolvedAt < ResolveInterval)
            {
                return _resolved;
            }

            await _resolveGate.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_clock.GetUtcNow() - _resolvedAt < ResolveInterval)
                {
                    return _resolved;
                }

                var result = new List<Uri>();

                foreach (var peer in _peers)
                {
                    ParsePeer(peer, out var uri, out var dns);

                    if (uri is not null)
                    {
                        result.Add(uri);
                        continue;
                    }

                    try
                    {
                        foreach (var address in await _resolve(dns.Host).ConfigureAwait(false))
                        {
                            if (_localAddresses.Contains(address))
                            {
                                continue;
                            }

                            var host = address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
                            result.Add(new Uri($"{dns.Scheme}://{host}:{dns.Port}/"));
                        }
                    }
                    catch (SocketException ex)
                    {
                        // keeps the previous answer: an instance does not vanish because dns hiccupped
                        _log?.Warning($"Cluster peers '{dns.Host}' could not be resolved: {ex.Message}");
                        result.AddRange(_resolved.Where(x => x.Host != dns.Host));
                    }
                }

                _resolved = result.Distinct().ToList();
                _resolvedAt = _clock.GetUtcNow();

                return _resolved;
            }
            finally
            {
                _resolveGate.Release();
            }
        }

        /// <summary>
        /// Splits a peer entry into a fixed uri or a dns name to resolve.
        /// </summary>
        /// <param name="peer">The configured entry.</param>
        /// <param name="uri">The fixed base uri, or null for a dns entry.</param>
        /// <param name="dns">The scheme, host and port to resolve, or default for a fixed uri.</param>
        private static void ParsePeer(string peer, out Uri uri, out (string Scheme, string Host, int Port) dns)
        {
            uri = null;
            dns = default;

            if (!Uri.TryCreate(peer, UriKind.Absolute, out var parsed) || string.IsNullOrEmpty(parsed.Host))
            {
                throw new ArgumentException($"The cluster peer '{peer}' is neither a base uri nor a dns:// entry.");
            }

            if (parsed.Scheme is "dns" or "dnss")
            {
                if (parsed.IsDefaultPort || parsed.Port <= 0)
                {
                    throw new ArgumentException($"The cluster peer '{peer}' needs a port, e.g. dns://service:8080.");
                }

                // dnss selects https towards the resolved instances
                dns = (parsed.Scheme == "dnss" ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, parsed.Host, parsed.Port);

                return;
            }

            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException($"The cluster peer '{peer}' must use http, https, dns or dnss.");
            }

            uri = parsed;
        }

        /// <summary>
        /// Computes the signature of a body with the shared secret.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The signature.</returns>
        private byte[] Sign(byte[] body)
        {
            return HMACSHA256.HashData(_secret, body);
        }

        /// <summary>
        /// Computes the signature of a body as the header value.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The Base64 encoded signature.</returns>
        private string SignBase64(byte[] body) => Convert.ToBase64String(Sign(body));

        /// <summary>
        /// Forgets message ids whose replay window has passed.
        /// </summary>
        /// <param name="now">The current time.</param>
        private void PruneSeen(DateTimeOffset now)
        {
            if (_seen.Count < 1024)
            {
                return;
            }

            foreach (var item in _seen)
            {
                if (item.Value < now)
                {
                    _seen.TryRemove(item);
                }
            }
        }

        /// <summary>
        /// Collects the addresses of this host, so a dns answer listing this instance is skipped.
        /// </summary>
        /// <returns>The local unicast addresses.</returns>
        private static HashSet<IPAddress> LocalAddresses()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                    .Select(x => x.Address)
                    .ToHashSet();
            }
            catch (NetworkInformationException)
            {
                return [];
            }
        }
    }
}
