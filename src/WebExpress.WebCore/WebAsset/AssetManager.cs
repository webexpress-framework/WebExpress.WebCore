using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAsset.Model;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// The asset manager manages WebExpress elements, which can be called with a URI (Uniform Resource Identifier).
    /// </summary>
    public sealed class AssetManager : IAssetManager, ISystemComponent
    {
        // synchronization root protecting _itemDictionary and related mutable state.
        // the dictionary itself is not thread-safe, so every read (including the
        // per-request resolution) and write must be performed under this lock.
        private readonly Lock _guard = new();

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly AssetItemDictionary _itemDictionary = new();

        /// <summary>
        /// An event that fires when an asset is added.
        /// </summary>
        public event EventHandler<IAssetContext> AddAsset;

        /// <summary>
        /// An event that fires when an asset is removed.
        /// </summary>
        public event EventHandler<IAssetContext> RemoveAsset;

        /// <summary>
        /// Gets all asset contexts.
        /// </summary>
        public IEnumerable<IAssetContext> Assets
        {
            get
            {
                // return a snapshot to avoid enumeration during concurrent modifications
                lock (_guard)
                {
                    return _itemDictionary.All.Select(x => x.AssetContext).ToList();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private AssetManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            var endpointtRegistration = new EndpointRegistration()
            {
                EndpointResolver = (type, applicationContext) => [],
                EndpointsResolver = () => Assets,
                HandleRequest = (request, endpointContext) =>
                {
                    var assetContext = endpointContext as IAssetContext;
                    AssetItem asset;

                    // resolve the asset under the lock so the lookup never enumerates
                    // the dictionary while it is being mutated by a (de)registration.
                    // prefer the endpoint already resolved by the sitemap; only fall
                    // back to a route match if it is not an asset context.
                    lock (_guard)
                    {
                        asset = _itemDictionary.GetByContext(assetContext)
                             ?? _itemDictionary.FindByRoute(request.Uri?.ToString());
                    }

                    // process outside the lock: Asset.Process only reads its immutable
                    // payload and builds a fresh response, so it must not block other
                    // asset requests.
                    if (asset?.Instance is not null)
                    {
                        return asset.Instance.Process(request);
                    }

                    return new ResponseNotFound();
                }
            };

            AddAsset += (sender, e) => endpointtRegistration.AddEndpoint?.Invoke(sender, e);
            RemoveAsset += (sender, e) => endpointtRegistration.RemoveEndpoint?.Invoke(sender, e);

            _componentHub?.EndpointManager.Register<AssetContext>(endpointtRegistration);

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:assetmanager.initialization")
            );
        }

        /// <summary>
        /// Discovers and binds resources to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose resources are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            lock (_guard)
            {
                if (_itemDictionary.ContainsPlugin(pluginContext))
                {
                    return;
                }
            }

            Register(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
        }

        /// <summary>
        /// Discovers and binds resources to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose resources are to be associated.</param>
        private void Register(IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub?.PluginManager?.GetPlugins(applicationContext))
            {
                bool alreadyRegistered;

                lock (_guard)
                {
                    alreadyRegistered = _itemDictionary.ContainsApplication(pluginContext, applicationContext);
                }

                if (alreadyRegistered)
                {
                    continue;
                }

                Register(pluginContext, [applicationContext]);
            }
        }

        /// <summary>
        /// Registers resources for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            // assembly and reflection operations are per-plugin and read-only; only the
            // mutation of _itemDictionary is synchronized.
            var assembly = pluginContext?.Assembly;
            var assemblName = assembly.GetName().Name;
            var embeddedResources = assembly.GetManifestResourceNames();

            foreach (var resource in embeddedResources)
            {
                if (resource.StartsWith(assemblName + ".Assets.", StringComparison.OrdinalIgnoreCase))
                {
                    var id = resource[(assemblName.Length + 8)..]?.Replace('\\', '/');

                    // assign the asset to existing applications
                    foreach (var applicationContext in applicationContexts)
                    {
                        var prefix = GetPluginRoute(applicationContext, pluginContext)
                            .Concat(new UriPathSegmentConstant("assets"));

                        var assetContext = new AssetContext()
                        {
                            EndpointId = new ComponentId($"{pluginContext.PluginId}.{id.Replace('/', '.')}"),
                            PluginContext = pluginContext,
                            ApplicationContext = applicationContext,
                            Route = prefix.Concat(id),
                            IncludeSubPaths = false
                        };

                        var assetItem = new AssetItem(_componentHub?.AssetManager)
                        {
                            AssetClass = typeof(Asset),
                            AssetContext = assetContext,
                            Instance = ComponentActivator.CreateInstance<IAsset, IAssetContext>
                            (
                                typeof(Asset),
                                assetContext,
                                _httpServerContext,
                                _componentHub,
                                resource
                            )
                        };

                        bool added;

                        lock (_guard)
                        {
                            added = _itemDictionary.AddAssetItem(pluginContext, applicationContext, assetItem);
                        }

                        if (added)
                        {
                            OnAddAsset(assetContext);
                            _httpServerContext?.Log?.Debug(
                                I18N.Translate(
                                    "webexpress.webcore:assetmanager.addresource",
                                    id,
                                    applicationContext.ApplicationId
                                )
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the route a plugin's endpoints are addressed under within an application.
        /// A plugin that owns the application serves straight from the application route,
        /// while every other plugin contributing to that application is given a segment of
        /// its own so two plugins cannot collide on the same file name.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="pluginContext">The context of the plugin.</param>
        /// <returns>The route the plugin's assets are mounted under.</returns>
        private static IRoute GetPluginRoute(IApplicationContext applicationContext, IPluginContext pluginContext)
        {
            return applicationContext.Route.Concat
            (
                applicationContext.PluginContext != pluginContext
                    ? pluginContext.PluginId.ToString()
                    : null
            );
        }

        /// <summary>
        /// Returns the route the given embedded file of a plugin is served from within an
        /// application. Consumers that have to link an embedded file - an include rendering a
        /// link or script element - resolve it here instead of composing the route themselves,
        /// because a route composed independently drifts from the mount without anything
        /// noticing: the browser answers the resulting 404 with the html error page and accepts
        /// it as a stylesheet with no rules.
        /// </summary>
        /// <param name="applicationContext">The context of the application the file is addressed in.</param>
        /// <param name="pluginContext">The context of the plugin the file belongs to.</param>
        /// <param name="file">The file path relative to the plugin's mount, as declared on the asset attribute.</param>
        /// <returns>The route of the file, or null when the arguments do not describe one.</returns>
        public IRoute GetAssetRoute(IApplicationContext applicationContext, IPluginContext pluginContext, string file)
        {
            if (applicationContext is null || pluginContext is null || string.IsNullOrWhiteSpace(file))
            {
                return null;
            }

            return GetPluginRoute(applicationContext, pluginContext).Concat(file);
        }

        /// <summary>
        /// Removes all resources associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the resources to remove.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            List<IAssetContext> removed;

            lock (_guard)
            {
                removed = _itemDictionary.Remove(pluginContext).ToList();
            }

            foreach (var assetContext in removed)
            {
                OnRemoveAsset(assetContext);
            }
        }

        /// <summary>
        /// Removes all assets associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the resources to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            List<IAssetContext> removed;

            lock (_guard)
            {
                removed = _itemDictionary.Remove(applicationContext).ToList();
            }

            foreach (var assetContext in removed)
            {
                OnRemoveAsset(assetContext);
            }
        }

        /// <summary>
        /// Returns an enumeration of all containing asset contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose asset are to be registered.</param>
        /// <returns>An enumeration of asset contexts.</returns>
        public IEnumerable<IAssetContext> GetAssets(IPluginContext pluginContext)
        {
            lock (_guard)
            {
                return _itemDictionary.GetAssets(pluginContext).ToList();
            }
        }

        /// <summary>
        /// Returns an enumeration of asset contextes.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of asset contextes.</returns>
        public IEnumerable<IAssetContext> GetAssets(IApplicationContext applicationContext)
        {
            lock (_guard)
            {
                return _itemDictionary.GetAssets(applicationContext).ToList();
            }
        }

        /// <summary>
        /// Raises the AddAsset event.
        /// </summary>
        /// <param name="assetContext">The asset context.</param>
        private void OnAddAsset(IAssetContext assetContext)
        {
            AddAsset?.Invoke(this, assetContext);
        }

        /// <summary>
        /// Raises the RemoveAsset event.
        /// </summary>
        /// <param name="assetContext">The asset context.</param>
        private void OnRemoveAsset(IAssetContext assetContext)
        {
            RemoveAsset?.Invoke(this, assetContext);
        }

        /// <summary>
        /// Raises the event when an plugin is added.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the plugin being added.</param>
        private void OnAddPlugin(object sender, IPluginContext e)
        {
            Register(e);
        }

        /// <summary>
        /// Raises the event when a plugin is removed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the plugin being removed.</param>
        private void OnRemovePlugin(object sender, IPluginContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Raises the event when an application is added.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the application being added.</param>
        private void OnAddApplication(object sender, IApplicationContext e)
        {
            Register(e);
        }

        /// <summary>
        /// Raises the event when an application is removed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the application being removed.</param>
        private void OnRemoveApplication(object sender, IApplicationContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            _componentHub?.PluginManager?.AddPlugin -= OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin -= OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication -= OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication -= OnRemoveApplication;
        }
    }
}
