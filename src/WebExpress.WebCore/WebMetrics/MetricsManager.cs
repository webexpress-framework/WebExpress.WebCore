using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMetrics.Model;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Discovers metric components through plugin and application events, keeps the shared
    /// instruments, and merges both with the framework series into one consistent scrape.
    /// </summary>
    public class MetricsManager : IMetricsManager, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly Lock _sync = new();
        private readonly List<MetricItem> _items = [];
        private readonly List<MetricInstrument> _instruments = [];
        private bool _disposed;

        /// <summary>
        /// Occurs when a discovered component is bound to an application.
        /// </summary>
        public event EventHandler<IMetricContext> AddMetric;

        /// <summary>
        /// Occurs when application or plugin removal detaches a metric component.
        /// </summary>
        public event EventHandler<IMetricContext> RemoveMetric;

        /// <summary>
        /// Gets the series the framework records itself, for the request pipeline and identity management.
        /// </summary>
        internal FrameworkMetrics Framework { get; }

        /// <summary>
        /// Gets a snapshot of all discovered component bindings without activating them.
        /// </summary>
        public IEnumerable<IMetricContext> Metrics
        {
            get
            {
                lock (_sync)
                {
                    return _items.Select(x => x.MetricContext).ToArray();
                }
            }
        }

        /// <summary>
        /// Gets a snapshot of the instruments exported on every scrape, including those of the framework.
        /// </summary>
        public IEnumerable<MetricInstrument> Instruments
        {
            get
            {
                lock (_sync)
                {
                    return _instruments.ToArray();
                }
            }
        }

        /// <summary>
        /// Connects discovery and component ownership to the same lifecycle events used by health checks.
        /// </summary>
        /// <param name="componentHub">The component hub supplying plugin and application associations.</param>
        /// <param name="httpServerContext">The host context supplying configuration and private logging.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private MetricsManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
            Framework = new FrameworkMetrics(componentHub);

            // registered so that an application can record into a framework series by name,
            // e.g. a login form of its own counting failed logins
            _instruments.AddRange(Framework.Instruments);

            _componentHub.PluginManager.AddPlugin += OnAddPlugin;
            _componentHub.PluginManager.RemovePlugin += OnRemovePlugin;
            _componentHub.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub.ApplicationManager.RemoveApplication += OnRemoveApplication;
        }

        /// <summary>
        /// Provides application-specific component metadata without activating the components.
        /// </summary>
        /// <param name="applicationContext">The application whose bindings are requested.</param>
        /// <returns>A snapshot of the metric components bound to the application.</returns>
        public IEnumerable<IMetricContext> GetMetrics(IApplicationContext applicationContext)
        {
            return Metrics.Where(x => x.ApplicationContext == applicationContext).ToArray();
        }

        /// <summary>
        /// Returns the counter with the given name, creating it on first use.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools; ignored when the counter exists.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <returns>The shared counter.</returns>
        public MetricCounter CreateCounter(string name, string help, params string[] labelNames)
        {
            return GetOrCreate(name, labelNames, null, () => new MetricCounter(name, help, labelNames));
        }

        /// <summary>
        /// Returns the gauge with the given name, creating it on first use.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools; ignored when the gauge exists.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <returns>The shared gauge.</returns>
        public MetricGauge CreateGauge(string name, string help, params string[] labelNames)
        {
            return GetOrCreate(name, labelNames, null, () => new MetricGauge(name, help, labelNames));
        }

        /// <summary>
        /// Returns the histogram with the given name, creating it on first use.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools; ignored when the histogram exists.</param>
        /// <param name="buckets">The upper bucket bounds, or null for the default buckets.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <returns>The shared histogram.</returns>
        public MetricHistogram CreateHistogram(string name, string help, IEnumerable<double> buckets, params string[] labelNames)
        {
            var bounds = (buckets ?? MetricHistogram.DefaultBuckets).ToArray();
            return GetOrCreate(name, labelNames, x => x.Buckets.SequenceEqual(bounds),
                () => new MetricHistogram(name, help, bounds, labelNames));
        }

        /// <summary>
        /// Shares one instrument per name, refusing a declaration that would make the series of the
        /// existing instrument ambiguous.
        /// </summary>
        /// <typeparam name="TInstrument">The instrument type.</typeparam>
        /// <param name="name">The metric name.</param>
        /// <param name="labelNames">The declared label names.</param>
        /// <param name="matches">An additional condition the existing instrument must meet, or null.</param>
        /// <param name="create">Creates the instrument on first use.</param>
        /// <returns>The shared instrument.</returns>
        /// <exception cref="InvalidOperationException">The name is taken by an incompatible instrument.</exception>
        private TInstrument GetOrCreate<TInstrument>(string name, string[] labelNames, Func<TInstrument, bool> matches,
            Func<TInstrument> create)
            where TInstrument : MetricInstrument
        {
            lock (_sync)
            {
                var existing = _instruments.FirstOrDefault(x => x.Name == name);
                if (existing is null)
                {
                    var instrument = create();
                    _instruments.Add(instrument);
                    return instrument;
                }

                if (existing is TInstrument typed && typed.LabelNames.SequenceEqual(labelNames ?? []) && (matches?.Invoke(typed) ?? true))
                {
                    return typed;
                }

                throw new InvalidOperationException($"The metric '{name}' is already declared as an incompatible " +
                    $"{existing.Type.ToString().ToLowerInvariant()} with the labels [{string.Join(", ", existing.LabelNames)}].");
            }
        }

        /// <summary>
        /// Binds each public sealed metric component once per contributing plugin and associated application.
        /// </summary>
        /// <param name="pluginContext">The plugin whose assembly supplies metric components.</param>
        /// <param name="applicationContexts">The applications associated with the plugin.</param>
        internal void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var metricTypes = pluginContext.Assembly.GetTypes()
                .Where(x => x.IsClass && x.IsPublic && x.IsSealed && !x.ContainsGenericParameters &&
                    typeof(IMetric).IsAssignableFrom(x)).ToArray();

            foreach (var applicationContext in applicationContexts.Distinct())
            {
                foreach (var metricType in metricTypes)
                {
                    MetricContext context;
                    lock (_sync)
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        if (_items.Any(x => x.MetricContext.PluginContext == pluginContext &&
                            x.MetricContext.ApplicationContext == applicationContext && x.MetricClass == metricType))
                        {
                            continue;
                        }

                        context = new MetricContext
                        {
                            PluginContext = pluginContext,
                            ApplicationContext = applicationContext,
                            MetricId = new ComponentId(metricType.FullName.ToLowerInvariant())
                        };
                        _items.Add(new MetricItem(metricType, context, _componentHub, _httpServerContext));
                    }

                    AddMetric?.Invoke(this, context);
                }
            }
        }

        /// <summary>
        /// Collects all sources concurrently, so one slow component costs the scrape at most its own budget.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the caller waiting for the scrape.</param>
        /// <returns>The merged metric families.</returns>
        /// <exception cref="ObjectDisposedException">The manager has been disposed.</exception>
        public async Task<IReadOnlyList<MetricFamily>> CollectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MetricItem[] items;
            MetricInstrument[] instruments;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                items = _items.ToArray();
                instruments = _instruments.ToArray();
            }

            var pending = Task.WhenAll(items.Select(x => x.CollectAsync(cancellationToken)));
            var set = new MetricSet();

            Merge(set, "webexpress", collector =>
            {
                Framework.ActiveUserWindow = ActiveUserWindow();
                Framework.Collect(collector);
            });

            Merge(set, "instruments", collector =>
            {
                foreach (var instrument in instruments)
                {
                    collector.Add(instrument);
                }
            });

            var results = await pending;
            var status = new MetricCollector();

            for (var i = 0; i < items.Length; i++)
            {
                if (results[i] is not null)
                {
                    Merge(set, results[i].Entries, items[i].MetricContext.MetricId.ToString());
                }

                status.Gauge("webexpress_metric_collector_up", "Whether a metric component reported its values in this scrape (1) or failed (0).",
                    results[i] is null ? 0 : 1,
                    new MetricLabel("application", items[i].MetricContext.ApplicationContext.ApplicationId ?? string.Empty),
                    new MetricLabel("collector", items[i].MetricContext.MetricId.ToString()));
            }

            Merge(set, status.Entries, "webexpress");

            return set.ToFamilies();
        }

        /// <summary>
        /// Runs a framework source in its own collector, so a failure there - such as process
        /// information a platform does not provide - only loses that source.
        /// </summary>
        /// <param name="set">The merged result.</param>
        /// <param name="source">The source name used in diagnostics.</param>
        /// <param name="collect">Reports the values of the source.</param>
        private void Merge(MetricSet set, string source, Action<IMetricCollector> collect)
        {
            var collector = new MetricCollector();

            try
            {
                collect(collector);
            }
            catch (Exception ex)
            {
                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:metrics.failed", "-", source, ex.Message));
                _httpServerContext.Log?.Exception(ex);
                return;
            }

            Merge(set, collector.Entries, source);
        }

        /// <summary>
        /// Adds the values of one source, dropping those that contradict an earlier source instead
        /// of letting them invalidate the whole scrape.
        /// </summary>
        /// <param name="set">The merged result.</param>
        /// <param name="entries">The values of the source.</param>
        /// <param name="source">The source name used in diagnostics.</param>
        private void Merge(MetricSet set, IEnumerable<MetricEntry> entries, string source)
        {
            foreach (var entry in entries)
            {
                if (!set.TryAdd(entry.Name, entry.Help, entry.Type, entry.Sample, out var conflict))
                {
                    _httpServerContext.Log?.Warning(I18N.Translate("webexpress.webcore:metrics.conflict", source, conflict));
                }
            }
        }

        /// <summary>
        /// Reads the active user window from the server settings at scrape time, since the settings
        /// are assigned to the host after the managers exist.
        /// </summary>
        /// <returns>The configured window, or five minutes when none is configured.</returns>
        private TimeSpan ActiveUserWindow()
        {
            var minutes = (_httpServerContext.Host as HttpServer)?.Settings?.Metrics?.ActiveUserWindowMinutes ?? 5;
            return TimeSpan.FromMinutes(Math.Max(1, minutes));
        }

        /// <summary>
        /// Removes a contributing plugin's components even when their applications belong to another plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin whose component bindings must be removed.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            Remove(x => x.MetricContext.PluginContext == pluginContext);
        }

        /// <summary>
        /// Detaches matching bindings before component disposal can execute application code.
        /// </summary>
        /// <param name="predicate">The ownership condition identifying bindings to remove.</param>
        private void Remove(Func<MetricItem, bool> predicate)
        {
            MetricItem[] removed;
            lock (_sync)
            {
                removed = _items.Where(predicate).ToArray();
                foreach (var item in removed)
                {
                    _items.Remove(item);
                }
            }

            foreach (var item in removed)
            {
                item.Dispose();
                RemoveMetric?.Invoke(this, item.MetricContext);
            }
        }

        /// <summary>
        /// Discovers components for applications associated with a newly available plugin.
        /// </summary>
        /// <param name="sender">The plugin manager raising the lifecycle event.</param>
        /// <param name="pluginContext">The plugin whose components are available.</param>
        private void OnAddPlugin(object sender, IPluginContext pluginContext)
        {
            Register(pluginContext, _componentHub.PluginManager.GetAssociatedApplications(pluginContext));
        }

        /// <summary>
        /// Discovers components from plugins already associated with a newly available application.
        /// </summary>
        /// <param name="sender">The application manager raising the lifecycle event.</param>
        /// <param name="applicationContext">The application whose metrics must be reported.</param>
        private void OnAddApplication(object sender, IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub.PluginManager.GetPlugins(applicationContext))
            {
                Register(pluginContext, [applicationContext]);
            }
        }

        /// <summary>
        /// Prevents an unloaded plugin from retaining metric component instances.
        /// </summary>
        /// <param name="sender">The plugin manager raising the lifecycle event.</param>
        /// <param name="pluginContext">The plugin whose bindings must be removed.</param>
        private void OnRemovePlugin(object sender, IPluginContext pluginContext)
        {
            Remove(pluginContext);
        }

        /// <summary>
        /// Prevents removed applications from leaving components contributed by any plugin in the host.
        /// </summary>
        /// <param name="sender">The application manager raising the lifecycle event.</param>
        /// <param name="applicationContext">The application whose bindings must be removed.</param>
        private void OnRemoveApplication(object sender, IApplicationContext applicationContext)
        {
            Remove(x => x.MetricContext.ApplicationContext == applicationContext);
        }

        /// <summary>
        /// Stops discovery and releases bindings while allowing active collections to finish before disposal.
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _componentHub.PluginManager.AddPlugin -= OnAddPlugin;
                _componentHub.PluginManager.RemovePlugin -= OnRemovePlugin;
                _componentHub.ApplicationManager.AddApplication -= OnAddApplication;
                _componentHub.ApplicationManager.RemoveApplication -= OnRemoveApplication;
            }

            Remove(_ => true);
            GC.SuppressFinalize(this);
        }
    }
}
