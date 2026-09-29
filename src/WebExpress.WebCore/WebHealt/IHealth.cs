using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebHealt
{
    /// <summary>
    /// Contributes a critical dependency check discovered and bound to applications through plugin lifecycle events.
    /// </summary>
    public interface IHealth : IComponent
    {
        /// <summary>
        /// Verifies that the dependency can support application requests without modifying business data.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token that expires when the check exceeds its budget.</param>
        /// <returns>The dependency's availability and diagnostic context reserved for server logs.</returns>
        Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken);
    }
}
