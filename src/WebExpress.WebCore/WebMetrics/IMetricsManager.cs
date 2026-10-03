using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Collects framework and application metrics for the global metrics endpoint, from which
    /// monitoring systems such as Prometheus derive dashboards, trends and alerts.
    /// </summary>
    /// <remarks>
    /// Applications contribute in two ways. Instruments created here record values where they occur
    /// and are exported as they are. Discovered <see cref="IMetric"/> components report values at
    /// scrape time, each labelled with the application it is bound to.
    /// </remarks>
    public interface IMetricsManager : IComponentManager
    {
        /// <summary>
        /// Occurs when a discovered component is bound to an application.
        /// </summary>
        event EventHandler<IMetricContext> AddMetric;

        /// <summary>
        /// Occurs when application or plugin removal detaches a metric component.
        /// </summary>
        event EventHandler<IMetricContext> RemoveMetric;

        /// <summary>
        /// Gets a snapshot of the discovered metric component bindings.
        /// </summary>
        IEnumerable<IMetricContext> Metrics { get; }

        /// <summary>
        /// Gets a snapshot of the instruments exported on every scrape, including those of the framework.
        /// </summary>
        IEnumerable<MetricInstrument> Instruments { get; }

        /// <summary>
        /// Provides application-specific component metadata without activating the components.
        /// </summary>
        /// <param name="applicationContext">The application whose bindings are requested.</param>
        /// <returns>A snapshot of the metric components bound to the application.</returns>
        IEnumerable<IMetricContext> GetMetrics(IApplicationContext applicationContext);

        /// <summary>
        /// Returns the counter with the given name, creating it on first use, so that independent
        /// parts of an application can record into the same series without sharing a reference.
        /// </summary>
        /// <param name="name">The metric name, by convention ending in <c>_total</c>.</param>
        /// <param name="help">The description shown by monitoring tools; ignored when the counter exists.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <returns>The shared counter.</returns>
        /// <exception cref="InvalidOperationException">The name is taken by an instrument of another type or with other labels.</exception>
        MetricCounter CreateCounter(string name, string help, params string[] labelNames);

        /// <summary>
        /// Returns the gauge with the given name, creating it on first use.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools; ignored when the gauge exists.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <returns>The shared gauge.</returns>
        /// <exception cref="InvalidOperationException">The name is taken by an instrument of another type or with other labels.</exception>
        MetricGauge CreateGauge(string name, string help, params string[] labelNames);

        /// <summary>
        /// Returns the histogram with the given name, creating it on first use.
        /// </summary>
        /// <param name="name">The metric name, by convention ending in the unit, such as <c>_seconds</c>.</param>
        /// <param name="help">The description shown by monitoring tools; ignored when the histogram exists.</param>
        /// <param name="buckets">The upper bucket bounds, or null for <see cref="MetricHistogram.DefaultBuckets"/>.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <returns>The shared histogram.</returns>
        /// <exception cref="InvalidOperationException">The name is taken by an instrument of another type, with other labels or other buckets.</exception>
        MetricHistogram CreateHistogram(string name, string help, IEnumerable<double> buckets, params string[] labelNames);

        /// <summary>
        /// Reads every framework series, every instrument and every discovered component. A failing
        /// or slow component only loses its own series and is reported through
        /// <c>webexpress_metric_collector_up</c>.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the caller waiting for the scrape.</param>
        /// <returns>The merged metric families.</returns>
        Task<IReadOnlyList<MetricFamily>> CollectAsync(CancellationToken cancellationToken = default);
    }
}
