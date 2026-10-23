using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Supplies application ownership and diagnostic identity to a discovered metric component.
    /// </summary>
    public class MetricContext : IMetricContext
    {
        /// <summary>
        /// Gets the plugin that contributes the metric component.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the application whose metrics the component reports.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the component identifier used in server diagnostics and in the collector status series.
        /// </summary>
        public IComponentId MetricId { get; internal set; }

        /// <summary>
        /// Gets the maximum time a scrape waits for this component. Shorter than a typical scrape
        /// timeout of ten seconds, so one slow component cannot cost the whole scrape.
        /// </summary>
        public TimeSpan Timeout { get; internal set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Creates metadata before component activation so constructor failures remain visible as a failed collector.
        /// </summary>
        public MetricContext()
        {
        }
    }
}
