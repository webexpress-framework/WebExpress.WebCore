using System;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// A message another instance of the cluster published. It carries the publishing instance
    /// so a receiver can tell its own echo apart and logs can name the source.
    /// </summary>
    /// <param name="Topic">The topic the message was published under.</param>
    /// <param name="Origin">The node id of the publishing instance.</param>
    /// <param name="Payload">The content, opaque to the transport.</param>
    /// <param name="Sent">When the publishing instance sent the message, by its own clock, or
    /// null when the transport does not carry the time. It reveals how far the clocks of the
    /// instances drift apart.</param>
    public sealed record ClusterMessage(string Topic, string Origin, byte[] Payload, DateTimeOffset? Sent = null);
}
