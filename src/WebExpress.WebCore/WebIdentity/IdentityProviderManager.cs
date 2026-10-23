using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Binds provider lifetimes to plugin and application events instead of the identity login path.
    /// </summary>
    public sealed class IdentityProviderManager : IIdentityProviderManager
    {
        private readonly IComponentHub _hub;
        private readonly IHttpServerContext _server;
        private readonly object _gate = new();
        private readonly List<Entry> _entries = [];

        /// <summary>
        /// Associates a provider instance with the application and plugin responsible for its lifetime.
        /// </summary>
        /// <param name="Provider">The authentication source owned by this registration.</param>
        /// <param name="Application">The application whose requests may use this source.</param>
        /// <param name="Plugin">The plugin whose removal ends this registration.</param>
        private sealed record Entry(IIdentityProvider Provider, IApplicationContext Application, IPluginContext Plugin);

        /// <summary>
        /// Connects authentication source ownership to plugin and application lifecycle events.
        /// </summary>
        /// <param name="componentHub">The hub supplying application-scoped authentication services.</param>
        /// <param name="httpServerContext">The server context supplying deployment configuration and framework services.</param>
        private IdentityProviderManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _hub = componentHub;
            _server = httpServerContext;
            _hub.PluginManager.AddPlugin += OnAddPlugin;
            _hub.PluginManager.RemovePlugin += OnRemovePlugin;
            _hub.ApplicationManager.AddApplication += OnAddApplication;
            _hub.ApplicationManager.RemoveApplication += OnRemoveApplication;
        }

        /// <summary>
        /// Returns an application-scoped snapshot that tolerates concurrent plugin changes.
        /// </summary>
        /// <param name="applicationContext">The application context that owns the requested operation.</param>
        /// <returns>A snapshot of authentication sources registered for the application.</returns>
        public IEnumerable<IIdentityProvider> GetProviders(IApplicationContext applicationContext)
        {
            lock (_gate) { return _entries.Where(x => x.Application == applicationContext).Select(x => x.Provider).ToArray(); }
        }

        /// <summary>
        /// Registers a configured source with its owning plugin so removal also removes authentication access.
        /// </summary>
        /// <param name="provider">The authentication source associated with an application.</param>
        /// <param name="applicationContext">The application context that owns the requested operation.</param>
        public void Register(IIdentityProvider provider, IApplicationContext applicationContext)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(applicationContext);
            var plugin = _hub.PluginManager.GetPlugins(applicationContext)
                .FirstOrDefault(x => x.Assembly == provider.GetType().Assembly) ?? applicationContext.PluginContext;
            lock (_gate)
            {
                if (!_entries.Any(x => x.Application == applicationContext && ReferenceEquals(x.Provider, provider)))
                {
                    _entries.Add(new Entry(provider, applicationContext, plugin));
                }
            }
        }

        /// <summary>
        /// Removes an explicitly configured source from subsequent authentication attempts.
        /// </summary>
        /// <param name="provider">The authentication source associated with an application.</param>
        /// <param name="applicationContext">The application context that owns the requested operation.</param>
        /// <returns>True when a registered provider binding was removed; otherwise, false.</returns>
        public bool Unregister(IIdentityProvider provider, IApplicationContext applicationContext)
        {
            lock (_gate) { return _entries.RemoveAll(x => x.Application == applicationContext && ReferenceEquals(x.Provider, provider)) > 0; }
        }

        /// <summary>
        /// Discovers sources for applications associated with a newly available plugin.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="plugin">The plugin whose authentication source bindings are affected.</param>
        private void OnAddPlugin(object sender, IPluginContext plugin)
        {
            foreach (var application in _hub.ApplicationManager.GetApplications(plugin)) { Discover(plugin, application); }
        }

        /// <summary>
        /// Discovers sources when an application becomes available after its plugins.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="application">The application whose provider bindings or authorization definitions apply.</param>
        private void OnAddApplication(object sender, IApplicationContext application)
        {
            foreach (var plugin in _hub.PluginManager.GetPlugins(application)) { Discover(plugin, application); }
        }

        /// <summary>
        /// Constructs each provider once per application using framework-supplied dependencies.
        /// </summary>
        /// <param name="plugin">The plugin whose authentication source bindings are affected.</param>
        /// <param name="application">The application whose provider bindings or authorization definitions apply.</param>
        private void Discover(IPluginContext plugin, IApplicationContext application)
        {
            foreach (var type in plugin.Assembly.GetExportedTypes().Where(x => x.IsClass && !x.IsAbstract &&
                !x.ContainsGenericParameters && typeof(IIdentityProvider).IsAssignableFrom(x)))
            {
                lock (_gate)
                {
                    if (_entries.Any(x => x.Plugin == plugin && x.Application == application && x.Provider.GetType() == type)) { continue; }
                    var dependencies = new object[] { _hub, _server, application, plugin };
                    var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .OrderByDescending(x => x.GetParameters().Length)
                        .FirstOrDefault(x => x.GetParameters().All(p => dependencies.Any(p.ParameterType.IsInstanceOfType)));
                    if (constructor is null) { continue; }
                    var provider = (IIdentityProvider)constructor.Invoke(constructor.GetParameters()
                        .Select(p => dependencies.First(p.ParameterType.IsInstanceOfType)).ToArray());
                    _entries.Add(new Entry(provider, application, plugin));
                }
            }
        }

        /// <summary>
        /// Removes authentication sources whose owning plugin is no longer available.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="plugin">The plugin whose authentication source bindings are affected.</param>
        private void OnRemovePlugin(object sender, IPluginContext plugin)
        {
            Remove(x => x.Plugin == plugin);
        }

        /// <summary>
        /// Removes provider bindings that no longer have an owning application.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="application">The application whose provider bindings or authorization definitions apply.</param>
        private void OnRemoveApplication(object sender, IApplicationContext application)
        {
            Remove(x => x.Application == application);
        }

        /// <summary>
        /// Disposes a removed source only when its final application binding disappears.
        /// </summary>
        /// <param name="predicate">The ownership condition selecting provider bindings for removal.</param>
        private void Remove(Predicate<Entry> predicate)
        {
            lock (_gate)
            {
                var removed = _entries.FindAll(predicate);
                _entries.RemoveAll(predicate);
                foreach (var provider in removed.Select(x => x.Provider).Distinct().OfType<IDisposable>())
                {
                    if (!_entries.Any(x => ReferenceEquals(x.Provider, provider))) { provider.Dispose(); }
                }
            }
        }

        /// <summary>
        /// Detaches lifecycle subscriptions and releases providers when the manager is shut down.
        /// </summary>
        public void Dispose()
        {
            _hub.PluginManager.AddPlugin -= OnAddPlugin;
            _hub.PluginManager.RemovePlugin -= OnRemovePlugin;
            _hub.ApplicationManager.AddApplication -= OnAddApplication;
            _hub.ApplicationManager.RemoveApplication -= OnRemoveApplication;
            Remove(_ => true);
        }
    }
}
