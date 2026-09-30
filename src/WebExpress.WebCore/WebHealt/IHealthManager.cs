using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebHealt
{
    /// <summary>
    /// Aggregates framework availability and application dependencies for the global health endpoint.
    /// </summary>
    public interface IHealthManager : IComponentManager
    {
        /// <summary>
        /// Occurs when a discovered component is bound to an application.
        /// </summary>
        event EventHandler<IHealthContext> AddHealth;

        /// <summary>
        /// Occurs when application or plugin removal detaches a health component.
        /// </summary>
        event EventHandler<IHealthContext> RemoveHealth;

        /// <summary>
        /// Gets a snapshot of the discovered health component bindings.
        /// </summary>
        IEnumerable<IHealthContext> HealthChecks { get; }

        /// <summary>
        /// Provides application-specific component metadata without activating dependency checks.
        /// </summary>
        /// <param name="applicationContext">The application whose bindings are requested.</param>
        /// <returns>A snapshot of the health components bound to the application.</returns>
        IEnumerable<IHealthContext> GetHealthChecks(IApplicationContext applicationContext);

        /// <summary>
        /// Requires every framework and application check to succeed without exposing diagnostic details.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the caller waiting for the probe.</param>
        /// <returns>True only when every critical component is available.</returns>
        Task<bool> CheckAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Judges the framework without running any application check, for probes that restart the process.
        /// </summary>
        /// <remarks>
        /// A liveness probe that fails while a database is down restarts every instance at once
        /// and cures nothing. The framework conditions are the ones a restart can clear: a host
        /// stuck in startup or shutdown, a component manager or a declared application that could
        /// not be created.
        /// </remarks>
        /// <returns>True when the framework is running and every declared application exists.</returns>
        bool CheckLiveness();
    }
}
