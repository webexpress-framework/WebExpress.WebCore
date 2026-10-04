using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// Carries live messages between the instances of a cluster. It exists for what a client
    /// must learn about right away - a popup, a data change, task progress - while the client is
    /// connected to an instance other than the one where it happened. Delivery is best effort:
    /// anything that must survive a lost message belongs in the <see cref="IClusterStore"/>.
    /// </summary>
    public interface IClusterTransport : IDisposable
    {
        /// <summary>
        /// Raised for every message another instance published.
        /// </summary>
        event EventHandler<ClusterMessage> Received;

        /// <summary>
        /// Sends a message to every other instance of the cluster.
        /// </summary>
        /// <param name="topic">The topic receivers dispatch on.</param>
        /// <param name="payload">The content.</param>
        /// <param name="cancellationToken">Cancels the delivery.</param>
        /// <returns>A task that completes once every instance was tried.</returns>
        Task SendAsync(string topic, byte[] payload, CancellationToken cancellationToken = default);
    }
}
