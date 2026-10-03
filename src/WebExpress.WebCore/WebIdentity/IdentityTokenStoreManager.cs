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
    /// Binds at most one token store to each application and ties the binding's lifetime to the owning
    /// plugin and application. Applications without a binding share the file store configured through
    /// <c>WebExpress:Authentication:TokenStorePath</c>, which is part of WebCore and needs no plugin.
    /// </summary>
    public sealed class IdentityTokenStoreManager : IIdentityTokenStoreManager
    {
        private readonly IComponentHub _hub;
        private readonly IHttpServerContext _server;
        private readonly object _gate = new();
        private readonly List<Entry> _entries = [];
        private FileIdentityTokenStore _defaultStore;

        /// <summary>
        /// Associates a store with the application it serves and the plugin responsible for its lifetime.
        /// </summary>
        /// <param name="Store">The store receiving the application's replay and revocation markers.</param>
        /// <param name="Application">The application whose credentials use this store.</param>
        /// <param name="Plugin">The plugin whose removal ends this binding.</param>
        /// <param name="Discovered">Whether the manager created the store and therefore owns its disposal on replacement.</param>
        private sealed record Entry(IIdentityTokenStore Store, IApplicationContext Application, IPluginContext Plugin, bool Discovered);

        /// <summary>
        /// Connects store ownership to plugin and application lifecycle events.
        /// </summary>
        /// <param name="componentHub">The hub supplying the plugin and application managers.</param>
        /// <param name="httpServerContext">The server context supplying the deployment's token-store location.</param>
        private IdentityTokenStoreManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _hub = componentHub;
            _server = httpServerContext;
            _hub.PluginManager.AddPlugin += OnAddPlugin;
            _hub.PluginManager.RemovePlugin += OnRemovePlugin;
            _hub.ApplicationManager.AddApplication += OnAddApplication;
            _hub.ApplicationManager.RemoveApplication += OnRemoveApplication;
        }

        /// <summary>
        /// Returns a snapshot of the bound stores and the default store, if it has been opened.
        /// </summary>
        public IEnumerable<IIdentityTokenStore> Stores
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Select(x => x.Store).Append(_defaultStore).Where(x => x is not null).Distinct().ToArray();
                }
            }
        }

        /// <summary>
        /// Resolves the application's binding, falling back to the shared file store.
        /// </summary>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        /// <returns>The store responsible for the application, or null when no durable storage is configured.</returns>
        public IIdentityTokenStore GetStore(IApplicationContext applicationContext)
        {
            if (applicationContext is null) { return null; }
            lock (_gate)
            {
                return _entries.FirstOrDefault(x => x.Application == applicationContext)?.Store ?? DefaultStore();
            }
        }

        /// <summary>
        /// Replaces the application's binding and attributes the store to the plugin that implements it.
        /// </summary>
        /// <param name="store">The store receiving the application's replay and revocation markers.</param>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        public void Register(IIdentityTokenStore store, IApplicationContext applicationContext)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(applicationContext);
            var plugin = _hub.PluginManager.GetPlugins(applicationContext)
                .FirstOrDefault(x => x.Assembly == store.GetType().Assembly) ?? applicationContext.PluginContext;
            lock (_gate)
            {
                var replaced = _entries.FindAll(x => x.Application == applicationContext && !ReferenceEquals(x.Store, store));
                _entries.RemoveAll(x => x.Application == applicationContext);
                _entries.Add(new Entry(store, applicationContext, plugin, false));
                // an explicitly registered store belongs to its caller until plugin or application removal
                Release(replaced.Where(x => x.Discovered));
            }
        }

        /// <summary>
        /// Removes an explicit binding without disposing the store, which remains owned by its caller.
        /// </summary>
        /// <param name="store">The store receiving the application's replay and revocation markers.</param>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        /// <returns>True when the binding existed and was removed; otherwise, false.</returns>
        public bool Unregister(IIdentityTokenStore store, IApplicationContext applicationContext)
        {
            lock (_gate)
            {
                return _entries.RemoveAll(x => x.Application == applicationContext && ReferenceEquals(x.Store, store)) > 0;
            }
        }

        /// <summary>
        /// Opens the shared file store on first use, so a deployment without a configured location stays unauthenticated
        /// instead of losing revocations in a temporary directory.
        /// </summary>
        /// <returns>The shared file store, or null when no location is configured.</returns>
        private FileIdentityTokenStore DefaultStore()
        {
            if (_defaultStore is not null) { return _defaultStore; }
            var path = _server?.Configuration?["WebExpress:Authentication:TokenStorePath"];
            if (string.IsNullOrWhiteSpace(path)) { return null; }
            _defaultStore = new FileIdentityTokenStore(path);
            return _defaultStore;
        }

        /// <summary>
        /// Discovers stores for applications associated with a newly available plugin.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="plugin">The plugin whose store bindings are affected.</param>
        private void OnAddPlugin(object sender, IPluginContext plugin)
        {
            foreach (var application in _hub.ApplicationManager.GetApplications(plugin)) { Discover(plugin, application); }
        }

        /// <summary>
        /// Discovers stores when an application becomes available after its plugins.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="application">The application whose store binding is affected.</param>
        private void OnAddApplication(object sender, IApplicationContext application)
        {
            foreach (var plugin in _hub.PluginManager.GetPlugins(application)) { Discover(plugin, application); }
        }

        /// <summary>
        /// Binds the first injectable store a plugin implements, leaving an existing binding untouched so
        /// load order cannot silently move an application's markers to a different store.
        /// </summary>
        /// <param name="plugin">The plugin whose store bindings are affected.</param>
        /// <param name="application">The application whose store binding is affected.</param>
        private void Discover(IPluginContext plugin, IApplicationContext application)
        {
            foreach (var type in plugin.Assembly.GetExportedTypes().Where(x => x.IsClass && !x.IsAbstract &&
                !x.ContainsGenericParameters && typeof(IIdentityTokenStore).IsAssignableFrom(x)))
            {
                lock (_gate)
                {
                    if (_entries.Any(x => x.Application == application)) { return; }
                    var dependencies = new object[] { _hub, _server, application, plugin };
                    var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .OrderByDescending(x => x.GetParameters().Length)
                        .FirstOrDefault(x => x.GetParameters().All(p => dependencies.Any(p.ParameterType.IsInstanceOfType)));
                    if (constructor is null) { continue; }
                    var store = (IIdentityTokenStore)constructor.Invoke(constructor.GetParameters()
                        .Select(p => dependencies.First(p.ParameterType.IsInstanceOfType)).ToArray());
                    _entries.Add(new Entry(store, application, plugin, true));
                }
            }
        }

        /// <summary>
        /// Removes stores whose owning plugin is no longer available.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="plugin">The plugin whose store bindings are affected.</param>
        private void OnRemovePlugin(object sender, IPluginContext plugin)
        {
            Remove(x => x.Plugin == plugin);
        }

        /// <summary>
        /// Removes the binding of an application that no longer exists.
        /// </summary>
        /// <param name="sender">The manager that raised the lifecycle event.</param>
        /// <param name="application">The application whose store binding is affected.</param>
        private void OnRemoveApplication(object sender, IApplicationContext application)
        {
            Remove(x => x.Application == application);
        }

        /// <summary>
        /// Unbinds and disposes the selected stores.
        /// </summary>
        /// <param name="predicate">The ownership condition selecting bindings for removal.</param>
        private void Remove(Predicate<Entry> predicate)
        {
            lock (_gate)
            {
                var removed = _entries.FindAll(predicate);
                _entries.RemoveAll(predicate);
                Release(removed);
            }
        }

        /// <summary>
        /// Disposes a store only once its last application binding is gone, because one instance may serve several applications.
        /// </summary>
        /// <param name="removed">The bindings that were just removed.</param>
        private void Release(IEnumerable<Entry> removed)
        {
            foreach (var store in removed.Select(x => x.Store).Distinct().OfType<IDisposable>())
            {
                if (!_entries.Any(x => ReferenceEquals(x.Store, store))) { store.Dispose(); }
            }
        }

        /// <summary>
        /// Detaches lifecycle subscriptions and releases the bound stores when the manager is shut down.
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
