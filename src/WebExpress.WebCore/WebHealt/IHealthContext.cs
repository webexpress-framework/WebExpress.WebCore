using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebHealt
{
    /// <summary>
    /// Identifies a discovered check and the application and plugin responsible for its lifetime.
    /// </summary>
    public interface IHealthContext : IContext
    {
        /// <summary>
        /// Gets the plugin that contributes the health component.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the application whose dependency the component verifies.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the component identifier used in server diagnostics.
        /// </summary>
        IComponentId HealthId { get; }

        /// <summary>
        /// Gets the maximum time a probe waits for this component.
        /// </summary>
        TimeSpan Timeout { get; }
    }
}
