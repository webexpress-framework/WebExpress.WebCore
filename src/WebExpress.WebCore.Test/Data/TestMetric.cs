using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMetrics;

namespace WebExpress.WebCore.Test.Data
{
    /// <summary>
    /// Exercises public sealed component discovery, dependency injection, and optional disposal.
    /// </summary>
    public sealed class TestMetric : IMetric, IDisposable
    {
        private readonly MetricTestPluginContext _plugin;

        /// <summary>
        /// Verifies that component activation supplies the binding and framework dependencies.
        /// </summary>
        /// <param name="metricContext">The plugin and application binding under test.</param>
        /// <param name="applicationContext">The application injected independently of the metric context.</param>
        /// <param name="componentHub">The hub supplying framework managers.</param>
        /// <param name="metricsManager">The shared manager injected through the component registry.</param>
        /// <param name="componentId">The discovered component identifier.</param>
        private TestMetric(IMetricContext metricContext, IApplicationContext applicationContext,
            IComponentHub componentHub, IMetricsManager metricsManager, IComponentId componentId)
        {
            Assert.Same(metricContext.ApplicationContext, applicationContext);
            Assert.Same(componentHub.MetricsManager, metricsManager);
            Assert.Same(metricContext.MetricId, componentId);
            _plugin = metricContext.PluginContext as MetricTestPluginContext;
            if (_plugin is not null)
            {
                lock (_plugin)
                {
                    _plugin.Created++;
                }

                if (_plugin.FailConstruction)
                {
                    throw new InvalidOperationException("private-constructor-diagnostic");
                }
            }
        }

        /// <summary>
        /// Executes configurable fixture behavior; under ordinary plugin discovery it reports one gauge.
        /// </summary>
        /// <param name="collector">The collector receiving the values.</param>
        /// <param name="cancellationToken">The budget token supplied by the metrics manager.</param>
        /// <returns>A task that completes once all values have been reported.</returns>
        public Task CollectAsync(IMetricCollector collector, CancellationToken cancellationToken)
        {
            if (_plugin is null)
            {
                collector.Gauge("test_metric_value", "A value reported by the test plugin.", 1);
                return Task.CompletedTask;
            }

            return _plugin.Collect(collector, cancellationToken);
        }

        /// <summary>
        /// Records cleanup so tests can detect premature or duplicate disposal.
        /// </summary>
        public void Dispose()
        {
            if (_plugin is not null)
            {
                lock (_plugin)
                {
                    _plugin.Disposed++;
                    _plugin.DisposedSignal.TrySetResult();
                }
            }
        }
    }

    /// <summary>
    /// Supplies a closed generic fixture type whose invalid timeout must surface as a failed collector.
    /// </summary>
    /// <typeparam name="T">The fixture type argument used to close the otherwise undiscoverable type.</typeparam>
    [MetricTimeout(0)]
    public sealed class InvalidTimeoutMetric<T> : IMetric
    {
        /// <summary>
        /// Fails the test if invalid component metadata is silently ignored.
        /// </summary>
        /// <param name="collector">The unused collector.</param>
        /// <param name="cancellationToken">The unused cancellation token.</param>
        /// <returns>No result because invalid metadata must prevent invocation.</returns>
        public Task CollectAsync(IMetricCollector collector, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid metadata must prevent execution.");
        }
    }

    /// <summary>
    /// Supplies an explicitly bounded component without polluting ordinary assembly discovery.
    /// </summary>
    /// <typeparam name="T">The fixture type argument used to close the component.</typeparam>
    [MetricTimeout(25)]
    public sealed class TimedMetric<T> : IMetric
    {
        /// <summary>
        /// Waits beyond the declared budget to demonstrate attribute-driven cancellation.
        /// </summary>
        /// <param name="collector">The unused collector.</param>
        /// <param name="cancellationToken">The token governed by the timeout attribute.</param>
        /// <returns>A task cancelled by the component's declared budget.</returns>
        public async Task CollectAsync(IMetricCollector collector, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    /// <summary>
    /// Ensures the public sealed discovery convention rejects extensible helper classes.
    /// </summary>
    public class UnsealedMetric : IMetric
    {
        /// <summary>
        /// Makes accidental discovery visible as an unexpected series.
        /// </summary>
        /// <param name="collector">The collector receiving the values.</param>
        /// <param name="cancellationToken">The unused cancellation token.</param>
        /// <returns>A completed task.</returns>
        public Task CollectAsync(IMetricCollector collector, CancellationToken cancellationToken)
        {
            collector.Gauge("unsealed_metric", "Must never be discovered.", 1);
            return Task.CompletedTask;
        }
    }
}
