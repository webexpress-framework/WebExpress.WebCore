using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebHealt.Model;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebHealt
{
    /// <summary>
    /// Discovers health components through plugin and application events and aggregates their availability.
    /// </summary>
    public class HealthManager : IHealthManager, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly Lock _sync = new();
        private readonly List<HealthItem> _items = [];
        private long _revision;
        private bool _disposed;

        /// <summary>
        /// Occurs when a discovered component is bound to an application.
        /// </summary>
        public event EventHandler<IHealthContext> AddHealth;

        /// <summary>
        /// Occurs when application or plugin removal detaches a health component.
        /// </summary>
        public event EventHandler<IHealthContext> RemoveHealth;

        /// <summary>
        /// Gets a snapshot of all discovered component bindings without activating them.
        /// </summary>
        public IEnumerable<IHealthContext> HealthChecks
        {
            get
            {
                lock (_sync)
                {
                    return _items.Select(x => x.HealthContext).ToArray();
                }
            }
        }

        /// <summary>
        /// Connects discovery and component ownership to the same lifecycle events used by fragments.
        /// </summary>
        /// <param name="componentHub">The component hub supplying plugin and application associations.</param>
        /// <param name="httpServerContext">The host context supplying lifecycle state and private logging.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private HealthManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
            _componentHub.PluginManager.AddPlugin += OnAddPlugin;
            _componentHub.PluginManager.RemovePlugin += OnRemovePlugin;
            _componentHub.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub.ApplicationManager.RemoveApplication += OnRemoveApplication;
        }

        /// <summary>
        /// Provides application-specific component metadata without activating dependency checks.
        /// </summary>
        /// <param name="applicationContext">The application whose bindings are requested.</param>
        /// <returns>A snapshot of the health components bound to the application.</returns>
        public IEnumerable<IHealthContext> GetHealthChecks(IApplicationContext applicationContext)
        {
            return HealthChecks.Where(x => x.ApplicationContext == applicationContext).ToArray();
        }

        /// <summary>
        /// Binds each public sealed health component once per contributing plugin and associated application.
        /// </summary>
        /// <param name="pluginContext">The plugin whose assembly supplies health components.</param>
        /// <param name="applicationContexts">The applications associated with the plugin.</param>
        internal void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var healthTypes = pluginContext.Assembly.GetTypes()
                .Where(x => x.IsClass && x.IsPublic && x.IsSealed && !x.ContainsGenericParameters &&
                    typeof(IHealth).IsAssignableFrom(x)).ToArray();

            foreach (var applicationContext in applicationContexts.Distinct())
            {
                foreach (var healthType in healthTypes)
                {
                    HealthContext context;
                    lock (_sync)
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        if (_items.Any(x => x.HealthContext.PluginContext == pluginContext &&
                            x.HealthContext.ApplicationContext == applicationContext && x.HealthClass == healthType))
                        {
                            continue;
                        }

                        context = new HealthContext
                        {
                            PluginContext = pluginContext,
                            ApplicationContext = applicationContext,
                            HealthId = new ComponentId(healthType.FullName.ToLowerInvariant())
                        };
                        _items.Add(new HealthItem(healthType, context, _componentHub, _httpServerContext));
                        _revision++;
                    }

                    AddHealth?.Invoke(this, context);
                }
            }
        }

        /// <summary>
        /// Evaluates every bound component concurrently so one failing dependency cannot hide another.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the caller waiting for the probe.</param>
        /// <returns>True only when the framework and every discovered dependency are available.</returns>
        public async Task<bool> CheckAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HealthItem[] items;
            long revision;
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }

                items = _items.ToArray();
                revision = _revision;
            }

            var frameworkHealthy = CheckFramework();
            var results = await Task.WhenAll(items.Select(x => x.CheckAsync(cancellationToken)));

            // lifecycle state may change while dependency checks are waiting
            lock (_sync)
            {
                return !_disposed && revision == _revision && frameworkHealthy && results.All(x => x) && CheckFramework();
            }
        }

        /// <summary>
        /// Rejects probes during startup or shutdown and when a component failed to initialize.
        /// </summary>
        /// <returns>True when hosting and the component registry can support application requests.</returns>
        private bool CheckFramework()
        {
            if (_httpServerContext.Host is not HttpServer { IsRunning: true })
            {
                _httpServerContext.Log?.Warning(I18N.Translate("webexpress.webcore:health.host_unavailable"));
                return false;
            }

            if (_componentHub.Managers.Any(x => x is null))
            {
                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:health.component_unavailable"));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Removes a contributing plugin's checks even when their applications belong to another plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin whose component bindings must be removed.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            Remove(x => x.HealthContext.PluginContext == pluginContext);
        }

        /// <summary>
        /// Detaches matching bindings before component disposal can execute application code.
        /// </summary>
        /// <param name="predicate">The ownership condition identifying bindings to remove.</param>
        private void Remove(Func<HealthItem, bool> predicate)
        {
            HealthItem[] removed;
            lock (_sync)
            {
                removed = _items.Where(predicate).ToArray();
                foreach (var item in removed)
                {
                    _items.Remove(item);
                    _revision++;
                }
            }

            foreach (var item in removed)
            {
                item.Dispose();
                RemoveHealth?.Invoke(this, item.HealthContext);
            }
        }

        /// <summary>
        /// Discovers checks for applications associated with a newly available plugin.
        /// </summary>
        /// <param name="sender">The plugin manager raising the lifecycle event.</param>
        /// <param name="pluginContext">The plugin whose components are available.</param>
        private void OnAddPlugin(object sender, IPluginContext pluginContext)
        {
            Register(pluginContext, _componentHub.PluginManager.GetAssociatedApplications(pluginContext));
        }

        /// <summary>
        /// Discovers checks from plugins already associated with a newly available application.
        /// </summary>
        /// <param name="sender">The application manager raising the lifecycle event.</param>
        /// <param name="applicationContext">The application whose dependencies must participate in probes.</param>
        private void OnAddApplication(object sender, IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub.PluginManager.GetPlugins(applicationContext))
            {
                Register(pluginContext, [applicationContext]);
            }
        }

        /// <summary>
        /// Prevents an unloaded plugin from retaining health component instances.
        /// </summary>
        /// <param name="sender">The plugin manager raising the lifecycle event.</param>
        /// <param name="pluginContext">The plugin whose bindings must be removed.</param>
        private void OnRemovePlugin(object sender, IPluginContext pluginContext)
        {
            Remove(pluginContext);
        }

        /// <summary>
        /// Prevents removed applications from leaving checks contributed by any plugin in the host.
        /// </summary>
        /// <param name="sender">The application manager raising the lifecycle event.</param>
        /// <param name="applicationContext">The application whose bindings must be removed.</param>
        private void OnRemoveApplication(object sender, IApplicationContext applicationContext)
        {
            Remove(x => x.HealthContext.ApplicationContext == applicationContext);
        }

        /// <summary>
        /// Stops discovery and releases bindings while allowing active checks to finish before disposal.
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
