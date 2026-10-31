using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// The single place that knows whether the server runs as one instance or as one of many,
    /// and that hands every subsystem the shared state and the message bus it needs to behave
    /// correctly behind a load balancer. Subsystems ask it instead of reading settings, so a
    /// plugin can swap the store or the transport (a database, a key-value server, a broker) for all of them.
    /// </summary>
    public interface IClusterManager : IComponentManager
    {
        /// <summary>
        /// Returns the id of this instance within the cluster.
        /// </summary>
        string NodeId { get; }

        /// <summary>
        /// Determines whether other instances share the state or receive the messages. While
        /// false, every subsystem keeps its single-instance behavior.
        /// </summary>
        bool IsClustered { get; }

        /// <summary>
        /// Returns the store holding the state every instance must see.
        /// </summary>
        IClusterStore Store { get; }

        /// <summary>
        /// Returns the transport forwarding messages to the other instances, or null when there
        /// are none to forward to.
        /// </summary>
        IClusterTransport Transport { get; }

        /// <summary>
        /// Returns, for every instance that sent a message, how far its clock ran ahead of this
        /// instance's (negative when behind), as measured on its last message. The measurement
        /// includes the transport latency, which is milliseconds; anything beyond a few seconds is
        /// a clock out of sync.
        /// </summary>
        IReadOnlyDictionary<string, TimeSpan> ClockSkew { get; }

        /// <summary>
        /// Replaces the store, so a plugin can supply a distributed one. Must happen while the
        /// plugin boots - before requests are served - since state written to the previous store
        /// is not carried over.
        /// </summary>
        /// <param name="store">The new store.</param>
        void UseStore(IClusterStore store);

        /// <summary>
        /// Replaces the transport, so a plugin can route messages through a broker.
        /// </summary>
        /// <param name="transport">The new transport, or null to stop forwarding messages.</param>
        void UseTransport(IClusterTransport transport);

        /// <summary>
        /// Sends a message to every other instance. Instances that are unreachable miss it.
        /// </summary>
        /// <param name="topic">The topic subscribers listen on.</param>
        /// <param name="payload">The content.</param>
        /// <param name="cancellationToken">Cancels the delivery.</param>
        /// <returns>A task that completes once every instance was tried.</returns>
        Task PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Listens for messages other instances publish on a topic. Messages this instance
        /// publishes are not delivered back to it; the publisher handles its own clients directly.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <param name="handler">Receives each message.</param>
        /// <returns>A handle that stops the subscription when disposed.</returns>
        IDisposable Subscribe(string topic, Action<ClusterMessage> handler);

        /// <summary>
        /// Takes a cluster-wide lock, so an operation that rewrites shared files - the package
        /// catalog - never interleaves with the same operation on another instance.
        /// </summary>
        /// <param name="name">The name of the lock.</param>
        /// <param name="lifetime">How long the lock holds when its owner dies without releasing it.</param>
        /// <param name="timeout">How long to wait for the lock.</param>
        /// <returns>A handle that releases the lock when disposed, or null when the timeout passed.</returns>
        IDisposable Lock(string name, TimeSpan lifetime, TimeSpan timeout);
    }
}
