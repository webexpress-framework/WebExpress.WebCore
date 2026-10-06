using System.Collections.Generic;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Settings for running several instances of the server side by side behind one load
    /// balancer. Without this block every instance keeps its state - sessions, job runs, popup
    /// notifications, login lockouts - to itself, which is only correct while there is exactly
    /// one instance. The block is optional so a single-instance deployment never changes
    /// behavior it did not opt into.
    /// </summary>
    public sealed class ClusterSettings
    {
        /// <summary>
        /// The name of the instance within the cluster. Left unset, the host name is used, which
        /// container runtimes set to the container or pod name - unique per instance and stable
        /// for its lifetime, which is exactly what job claims and log lines need.
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// The directory holding the state every instance must see: sessions, job claims, login
        /// lockouts, global notifications and chat history. It must be a volume all instances
        /// mount (for example a ReadWriteMany volume) that supports atomic renames and exclusive
        /// creation. Left unset, the state stays in memory and the deployment is single-instance
        /// unless a plugin supplies a distributed store.
        /// </summary>
        public string StatePath { get; set; }

        /// <summary>
        /// The other instances that live messages - popup notifications, data changes, task
        /// progress, chat - are forwarded to. An entry is either a base uri
        /// (<c>http://10.0.0.5:8080/</c>) or a dns name whose address records list every instance
        /// (<c>dns://webexpress-headless:8080</c>), as a Kubernetes headless service or the
        /// service name in Docker Compose provides. Left empty, messages reach only the clients
        /// connected to the instance that raised them.
        /// </summary>
        public List<string> Peers { get; set; } = [];

        /// <summary>
        /// The shared secret, Base64 encoded with at least 256 random bits, that authenticates
        /// messages between instances. Required as soon as <see cref="Peers"/> is set, because
        /// the receiving endpoint sits on the public listener and would otherwise accept forged
        /// popups from anyone who can reach it.
        /// </summary>
        public string Secret { get; set; }

        /// <summary>
        /// An additional listener reserved for the messages between instances, e.g.
        /// <c>http://0.0.0.0:8081/</c>. Once set, the bus answers on this listener only and on
        /// nothing else, while the public endpoints no longer know its path - so a load balancer or
        /// ingress that only forwards the public port can never reach it. Peers must then address
        /// this port. Left unset, the bus shares the public listener and relies on its signature.
        /// </summary>
        public string Listen { get; set; }

        /// <summary>
        /// Returns the port of the internal listener.
        /// </summary>
        /// <returns>The port, or null when no internal listener is configured.</returns>
        internal int? GetListenPort()
        {
            return string.IsNullOrWhiteSpace(Listen) ? null : new EndpointSettings { Uri = Listen }.GetBindingAddress().Port;
        }
    }
}
