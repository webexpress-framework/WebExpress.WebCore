using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebMetrics.Model
{
    /// <summary>
    /// Owns one application binding and shares an unfinished collection between concurrent scrapes,
    /// so several Prometheus replicas do not multiply the work of a slow component.
    /// </summary>
    internal sealed class MetricItem : IDisposable
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly Exception _activationError;
        private readonly Lock _sync = new();
        private IMetric _instance;
        private Task<MetricCollector> _operation;
        private bool _disposed;

        /// <summary>
        /// Gets the metadata retained even when component activation fails.
        /// </summary>
        public IMetricContext MetricContext { get; }

        /// <summary>
        /// Gets the discovered type used to distinguish bindings during lifecycle events.
        /// </summary>
        public Type MetricClass { get; }

        /// <summary>
        /// Keeps discovery separate from activation so failing constructors stay visible as a failed collector.
        /// </summary>
        /// <param name="metricClass">The discovered public sealed metric component.</param>
        /// <param name="metricContext">The plugin and application ownership of this binding.</param>
        /// <param name="componentHub">The component hub used for constructor injection.</param>
        /// <param name="httpServerContext">The host supplying private diagnostics and configuration.</param>
        public MetricItem(Type metricClass, MetricContext metricContext, IComponentHub componentHub,
            IHttpServerContext httpServerContext)
        {
            MetricClass = metricClass;
            MetricContext = metricContext;
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            try
            {
                var timeout = metricClass.GetCustomAttribute<MetricTimeoutAttribute>();
                if (timeout is not null)
                {
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout.Milliseconds);
                    metricContext.Timeout = TimeSpan.FromMilliseconds(timeout.Milliseconds);
                }
            }
            catch (Exception ex)
            {
                _activationError = ex;
            }
        }

        /// <summary>
        /// Limits scrape latency even when component construction or synchronous application code blocks.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for this scrape, independent of other scrapes.</param>
        /// <returns>The reported values, or null when the component failed or exceeded its budget.</returns>
        public async Task<MetricCollector> CollectAsync(CancellationToken cancellationToken)
        {
            Task<MetricCollector> operation;
            lock (_sync)
            {
                if (_disposed)
                {
                    return null;
                }

                if (_operation is null || _operation.IsCompleted)
                {
                    _operation = Task.Run(ExecuteAsync);
                }

                operation = _operation;
            }

            try
            {
                return await operation.WaitAsync(MetricContext.Timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:metrics.timeout",
                    MetricContext.ApplicationContext.ApplicationId, MetricContext.MetricId, MetricContext.Timeout));
                return null;
            }
        }

        /// <summary>
        /// Reuses one injected component per binding and observes exceptions after a scrape has timed out.
        /// </summary>
        /// <returns>The reported values, or null when the component failed.</returns>
        private async Task<MetricCollector> ExecuteAsync()
        {
            using var cancellation = new CancellationTokenSource(MetricContext.Timeout);
            try
            {
                if (_activationError is not null)
                {
                    throw new InvalidOperationException("The metric component has invalid configuration.", _activationError);
                }

                _instance ??= ComponentActivator.CreateInstance<IMetric, IMetricContext>
                (
                    MetricClass, MetricContext, _httpServerContext, _componentHub, MetricContext.ApplicationContext
                ) ?? throw new InvalidOperationException("The metric component could not be created.");

                var collector = new MetricCollector(new MetricLabel("application", MetricContext.ApplicationContext.ApplicationId ?? string.Empty));
                await (_instance.CollectAsync(collector, cancellation.Token)
                    ?? throw new InvalidOperationException("The metric component returned no task."));
                cancellation.Token.ThrowIfCancellationRequested();

                return collector;
            }
            catch (Exception ex)
            {
                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:metrics.failed",
                    MetricContext.ApplicationContext.ApplicationId, MetricContext.MetricId, ex.GetBaseException().Message));
                _httpServerContext.Log?.Exception(ex);
            }

            return null;
        }

        /// <summary>
        /// Prevents new invocations and defers resource disposal until an active invocation finishes.
        /// </summary>
        public void Dispose()
        {
            Task<MetricCollector> operation;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                operation = _operation;
            }

            _ = DisposeAfterExecutionAsync(operation);
        }

        /// <summary>
        /// Releases component resources without racing a collection that is still using them.
        /// </summary>
        /// <param name="operation">The last invocation, or null when the component was never activated.</param>
        /// <returns>A task that completes after the component's resources have been released.</returns>
        private async Task DisposeAfterExecutionAsync(Task<MetricCollector> operation)
        {
            try
            {
                if (operation is not null)
                {
                    await operation;
                }
            }
            finally
            {
                try
                {
                    (_instance as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    _httpServerContext.Log?.Exception(ex);
                }
            }
        }
    }
}
