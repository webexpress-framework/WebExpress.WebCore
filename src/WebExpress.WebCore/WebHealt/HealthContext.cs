using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebHealt
{
    /// <summary>
    /// Supplies application ownership and diagnostic identity to a discovered health component.
    /// </summary>
    public class HealthContext : IHealthContext
    {
        /// <summary>
        /// Gets the plugin that contributes the health component.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the application whose dependency the component verifies.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the component identifier used in server diagnostics.
        /// </summary>
        public IComponentId HealthId { get; internal set; }

        /// <summary>
        /// Gets the maximum time a probe waits for this component.
        /// </summary>
        public TimeSpan Timeout { get; internal set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Creates metadata before component activation so constructor failures remain registered as unhealthy checks.
        /// </summary>
        public HealthContext()
        {
        }
    }
}
