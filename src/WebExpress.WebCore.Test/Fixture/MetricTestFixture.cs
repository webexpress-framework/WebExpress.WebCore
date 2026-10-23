using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMetrics;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.Test.Fixture
{
    /// <summary>
    /// Supplies isolated plugin assemblies and collection behavior while exercising real component discovery.
    /// </summary>
    internal static class MetricTestFixture
    {
        /// <summary>
        /// Binds a configurable test component through the same discovery method used by lifecycle events.
        /// </summary>
        /// <param name="manager">The metrics manager under test.</param>
        /// <param name="application">The application owning the test component.</param>
        /// <param name="name">The diagnostic identifier of the isolated contributing plugin.</param>
        /// <param name="collect">The behavior executed by the discovered test component.</param>
        /// <param name="timeout">An optional short budget for testing timeout behavior.</param>
        /// <returns>The plugin binding whose disposal simulates plugin removal.</returns>
        internal static MetricTestRegistration Register(IMetricsManager manager, IApplicationContext application,
            string name, Func<IMetricCollector, CancellationToken, Task> collect, TimeSpan? timeout = null)
        {
            var plugin = new MetricTestPluginContext(name, typeof(TestMetric)) { Collect = collect };
            ((MetricsManager)manager).Register(plugin, [application]);
            var context = manager.Metrics.SingleOrDefault(x => x.PluginContext == plugin);
            if (timeout.HasValue && context is MetricContext metadata)
            {
                metadata.Timeout = timeout.Value;
            }

            return new MetricTestRegistration((MetricsManager)manager, plugin);
        }

        /// <summary>
        /// Renders a scrape the way the endpoint does, so assertions read like the exposition text.
        /// </summary>
        /// <param name="manager">The metrics manager to scrape.</param>
        /// <returns>The exposition text.</returns>
        internal static async Task<string> ScrapeAsync(IMetricsManager manager)
        {
            var families = await manager.CollectAsync(TestContext.Current.CancellationToken);
            return WebMetrics.Model.MetricText.Format(families);
        }
    }

    /// <summary>
    /// Associates controllable collection behavior with an otherwise normal plugin context.
    /// </summary>
    internal sealed class MetricTestPluginContext : PluginContext
    {
        /// <summary>
        /// Gets or sets the behavior executed by the test component.
        /// </summary>
        internal Func<IMetricCollector, CancellationToken, Task> Collect { get; set; } = (_, _) => Task.CompletedTask;

        /// <summary>
        /// Gets or sets whether component construction must fail before a collection can execute.
        /// </summary>
        internal bool FailConstruction { get; set; }

        /// <summary>
        /// Gets or sets the number of component instances created for this plugin.
        /// </summary>
        internal int Created { get; set; }

        /// <summary>
        /// Gets or sets the number of component instances disposed for this plugin.
        /// </summary>
        internal int Disposed { get; set; }

        /// <summary>
        /// Signals that a component was released after its active collection finished.
        /// </summary>
        internal TaskCompletionSource DisposedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Restricts discovery to explicit fixture types without loading another physical assembly.
        /// </summary>
        /// <param name="name">The unique plugin identifier used by the test.</param>
        /// <param name="types">The component types exposed by this isolated assembly.</param>
        internal MetricTestPluginContext(string name, params Type[] types)
        {
            PluginId = new ComponentId(name);
            Assembly = new HealthTestAssembly(types);
        }
    }

    /// <summary>
    /// Simulates removal of an isolated contributing plugin without modifying other bindings.
    /// </summary>
    internal sealed class MetricTestRegistration : IDisposable
    {
        private readonly MetricsManager _manager;

        /// <summary>
        /// Gets the plugin state used to inspect activation and disposal.
        /// </summary>
        internal MetricTestPluginContext Plugin { get; }

        /// <summary>
        /// Associates cleanup with the manager that owns the test plugin's bindings.
        /// </summary>
        /// <param name="manager">The manager owning the bindings.</param>
        /// <param name="plugin">The isolated plugin removed during cleanup.</param>
        internal MetricTestRegistration(MetricsManager manager, MetricTestPluginContext plugin)
        {
            _manager = manager;
            Plugin = plugin;
        }

        /// <summary>
        /// Runs the manager's normal plugin removal path.
        /// </summary>
        public void Dispose()
        {
            _manager.Remove(Plugin);
        }
    }
}
