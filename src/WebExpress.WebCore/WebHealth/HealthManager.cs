using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebHealth.Model;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebHealth
{
    /// <summary>
    /// Discovers health components through plugin and application events and aggregates their availability.
    /// </summary>
    /// <remarks>
    /// The discovery is a <see cref="PluginTypeCatalog{TItem}"/> bound per application: one binding
    /// per public sealed <see cref="IHealth"/> type, contributing plugin and associated application.
    /// The key names the plugin and the type, so no two plugins ever shadow each other's checks.
    /// </remarks>
    public class HealthManager : IHealthManager, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly Lock _sync = new();
        private readonly PluginTypeCatalog<HealthItem> _catalog;
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
                return _catalog.Items.Select(x => x.HealthContext).ToArray();
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
            _catalog = new PluginTypeCatalog<HealthItem>(KeyOf, typeof(IHealth), Create, PluginTypeBinding.Application);
            _catalog.Added += (_, e) => AddHealth?.Invoke(this, e.Item.HealthContext);
            _catalog.Removed += (_, e) => RemoveHealth?.Invoke(this, e.Item.HealthContext);
            _catalog.Attach(componentHub, httpServerContext);
        }

        /// <summary>
        /// Keys a binding by its plugin and type, so checks of different plugins never replace
        /// each other even when their types share a name.
        /// </summary>
        /// <param name="item">The binding.</param>
        /// <returns>The key.</returns>
        private static string KeyOf(HealthItem item)
        {
            return $"{item.HealthContext.PluginContext?.PluginId}/{item.HealthClass.FullName}";
        }

        /// <summary>
        /// Builds a binding without activating the component, so a failing constructor surfaces as
        /// an unsuccessful check rather than as a missing one.
        /// </summary>
        /// <param name="origin">The discovered type and its plugin and application.</param>
        /// <returns>The binding.</returns>
        private HealthItem Create(PluginTypeOrigin origin)
        {
            var context = new HealthContext
            {
                PluginContext = origin.PluginContext,
                ApplicationContext = origin.ApplicationContext,
                HealthId = new ComponentId(origin.Type.FullName.ToLowerInvariant())
            };

            return new HealthItem(origin.Type, context, _componentHub, _httpServerContext);
        }

        /// <summary>
        /// Provides application-specific component metadata without activating dependency checks.
        /// </summary>
        /// <param name="applicationContext">The application whose bindings are requested.</param>
        /// <returns>A snapshot of the health components bound to the application.</returns>
        public IEnumerable<IHealthContext> GetHealthChecks(IApplicationContext applicationContext)
        {
            return _catalog.GetItems(applicationContext).Select(x => x.HealthContext).ToArray();
        }

        /// <summary>
        /// Binds each public sealed health component once per contributing plugin and associated application.
        /// </summary>
        /// <param name="pluginContext">The plugin whose assembly supplies health components.</param>
        /// <param name="applicationContexts">The applications associated with the plugin.</param>
        internal void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            _catalog.Collect(pluginContext, applicationContexts);
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

                revision = _catalog.Version;
                items = [.. _catalog.Items];
            }

            var frameworkHealthy = CheckFramework();
            var results = await Task.WhenAll(items.Select(x => x.CheckAsync(cancellationToken)));

            // lifecycle state may change while dependency checks are waiting
            lock (_sync)
            {
                return !_disposed && revision == _catalog.Version && frameworkHealthy && results.All(x => x) && CheckFramework();
            }
        }

        /// <summary>
        /// Judges the framework alone, so a liveness probe restarts the process only for faults a
        /// restart can cure and never because a database the process does not own is down.
        /// </summary>
        /// <returns>True when the framework is running and every declared application exists.</returns>
        public bool CheckLiveness()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }
            }

            return CheckFramework();
        }

        /// <summary>
        /// Rejects probes during startup or shutdown, when a component failed to initialize and
        /// when a declared application could not be created.
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

            // a failed application took its health bindings with it, so no check of its own can fail
            var failures = _componentHub.ApplicationManager.FailedApplications.ToArray();
            foreach (var failure in failures)
            {
                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:health.application_unavailable",
                    failure.ApplicationId, failure.PluginContext?.PluginId, failure.Exception?.Message));
            }

            return failures.Length == 0;
        }

        /// <summary>
        /// Removes a contributing plugin's checks even when their applications belong to another plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin whose component bindings must be removed.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            _catalog.Withdraw(pluginContext);
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
            }

            // detaches every binding and disposes it once its active check finished
            _catalog.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
