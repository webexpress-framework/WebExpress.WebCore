using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Contributes application metrics - database accesses, LDAP requests, queue lengths - to the
    /// global metrics endpoint. Components are discovered and bound to applications through plugin
    /// lifecycle events, the same way as health checks.
    /// </summary>
    public interface IMetric : IComponent
    {
        /// <summary>
        /// Reports the current values on every scrape. The call should only read state the
        /// application already keeps; a value that is expensive to obtain belongs in a background
        /// job whose result is reported here.
        /// </summary>
        /// <param name="collector">The collector receiving the values.</param>
        /// <param name="cancellationToken">The cancellation token that expires when the collection exceeds its budget.</param>
        /// <returns>A task that completes once all values have been reported.</returns>
        Task CollectAsync(IMetricCollector collector, CancellationToken cancellationToken);
    }
}
