using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Identifies a discovered metric component and the application and plugin responsible for its lifetime.
    /// </summary>
    public interface IMetricContext : IContext
    {
        /// <summary>
        /// Gets the plugin that contributes the metric component.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the application whose metrics the component reports.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the component identifier used in server diagnostics and in the collector status series.
        /// </summary>
        IComponentId MetricId { get; }

        /// <summary>
        /// Gets the maximum time a scrape waits for this component.
        /// </summary>
        TimeSpan Timeout { get; }
    }
}
