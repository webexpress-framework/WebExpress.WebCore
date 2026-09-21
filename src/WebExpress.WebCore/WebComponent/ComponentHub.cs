using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebComponent.Model;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebEvent;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebInclude;
using WebExpress.WebCore.WebJob;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPackage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebResource;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebSession;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebCore.WebSitemap;
using WebExpress.WebCore.WebSocket;
using WebExpress.WebCore.WebStatusPage;
using WebExpress.WebCore.WebTask;
using WebExpress.WebCore.WebTheme;

namespace WebExpress.WebCore.WebComponent
{
    /// <summary>
    /// Central management of components.
    /// </summary>
    public class ComponentHub : IComponentHub
    {
        private readonly IHttpServerContext _httpServerContext;
        private readonly ComponentDictionary _dictionary = [];
        private readonly LogManager _logManager;
        private readonly PackageManager _packageManager;
        private readonly InternationalizationManager _internationalizationManager;
        private readonly PluginManager _pluginManager;
        private readonly ApplicationManager _applicationManager;
        private readonly EndpointManager _endpointManager;
        private readonly AssetManager _assetManager;
        private readonly ResourceManager _resourceManager;
        private readonly IncludeManager _includeManager;
        private readonly PageManager _pageManager;
        private readonly SettingPageManager _settingPageManager;
        private readonly RestApiManager _restApiManager;
        private readonly SitemapManager _sitemapManager;
        private readonly FragmentManager _fragmentManager;
        private readonly StatusPageManager _statusPageManager;
        private readonly SessionManager _sessionManager;
        private readonly EventManager _eventManager;
        private readonly JobManager _jobManager;
        private readonly TaskManager _taskManager;
        private readonly IdentityManager _identityManager;
        private readonly IdentityProviderManager _identityProviderManager;
        private readonly SocketManager _socketManager;
        private readonly ThemeManager _themeManager;
        private int _lastCounter = 0;

        /// <summary>
        /// An event that fires when an component is added.
        /// </summary>
        public event EventHandler<IComponentManager> AddComponent;

        /// <summary>
        /// An event that fires when an component is removed.
        /// </summary>
        public event EventHandler<IComponentManager> RemoveComponent;

        /// <summary>
        /// Gets all registered managers.
        /// </summary>
        public IEnumerable<IComponentManager> Managers => new IComponentManager[]
            {
                _logManager,
                _packageManager,
                _pluginManager,
                _applicationManager,
                _endpointManager,
                _sitemapManager,
                _fragmentManager,
                _assetManager,
                _resourceManager,
                _includeManager,
                _pageManager,
                _settingPageManager,
                _restApiManager,
                _eventManager,
                _jobManager,
                _statusPageManager,
                _internationalizationManager,
                _identityManager,
                _identityProviderManager,
                _sessionManager,
                _taskManager,
                _socketManager,
                _themeManager
            }.Concat(_dictionary.Values.SelectMany(x => x).Select(x => x.ComponentInstance));

        /// <summary>
        /// Gets the log manager.
        /// </summary>
        /// <returns>The instance of the log manager.</returns>
        public ILogManager LogManager => _logManager;

        /// <summary>
        /// Gets the package manager.
        /// </summary>
        /// <returns>The instance of the package manager.</returns>
        public IPackageManager PackageManager => _packageManager;

        /// <summary>
        /// Gets the plugin manager.
        /// </summary>
        /// <returns>The instance of the plugin manager.</returns>
        public IPluginManager PluginManager => _pluginManager;

        /// <summary>
        /// Gets the application manager.
        /// </summary>
        /// <returns>The instance of the application manager.</returns>
        public IApplicationManager ApplicationManager => _applicationManager;

        /// <summary>
        /// Gets the event manager.
        /// </summary>
        /// <returns>The instance of the event manager.</returns>
        public IEventManager EventManager => _eventManager;

        /// <summary>
        /// Gets the job manager.
        /// </summary>
        /// <returns>The instance of the job manager.</returns>
        public IJobManager JobManager => _jobManager;

        /// <summary>
        /// Gets the task manager.
        /// </summary>
        /// <returns>The instance of the task manager.</returns>
        public ITaskManager TaskManager => _taskManager;

        /// <summary>
        /// Gets the endpoint manager.
        /// </summary>
        /// <returns>The instance of the endpoint manager.</returns>
        public IEndpointManager EndpointManager => _endpointManager;

        /// <summary>
        /// Gets the asset manager.
        /// </summary>
        /// <returns>The instance of the asset manager.</returns>
        public IAssetManager AssetManager => _assetManager;

        /// <summary>
        /// Gets the resource manager.
        /// </summary>
        /// <returns>The instance of the resource manager.</returns>
        public IResourceManager ResourceManager => _resourceManager;

        /// <summary>
        /// Gets the include manager.
        /// </summary>
        /// <returns>The instance of the include manager.</returns>
        public IIncludeManager IncludeManager => _includeManager;

        /// <summary>
        /// Gets the page manager.
        /// </summary>
        /// <returns>The instance of the page manager.</returns>
        public IPageManager PageManager => _pageManager;

        /// <summary>
        /// Gets the setting page manager.
        /// </summary>
        /// <returns>The instance of the setting page manager.</returns>
        public ISettingPageManager SettingPageManager => _settingPageManager;

        /// <summary>
        /// Gets the rest api manager.
        /// </summary>
        /// <returns>The instance of the rest api manager.</returns>
        public IRestApiManager RestApiManager => _restApiManager;

        /// <summary>
        /// Gets the sitemap manager.
        /// </summary>
        /// <returns>The instance of the sitemap manager.</returns>
        public ISitemapManager SitemapManager => _sitemapManager;

        /// <summary>
        /// Gets the fragment manager.
        /// </summary>
        /// <returns>The instance of the fragment manager.</returns>
        public IFragmentManager FragmentManager => _fragmentManager;

        /// <summary>
        /// Gets the status page manager.
        /// </summary>
        /// <returns>The instance of the status page manager.</returns>
        public IStatusPageManager StatusPageManager => _statusPageManager;

        /// <summary>
        /// Gets the internationalization manager.
        /// </summary>
        /// <returns>The instance of the internationalization manager.</returns>
        public IInternationalizationManager InternationalizationManager => _internationalizationManager;

        /// <summary>
        /// Gets the identity manager.
        /// </summary>
        /// <returns>The instance of the identity manager.</returns>
        public IIdentityManager IdentityManager => _identityManager;

        /// <summary>
        /// Keeps provider discovery independent of credential issuance.
        /// </summary>
        public IIdentityProviderManager IdentityProviderManager => _identityProviderManager;

        /// <summary>
        /// Gets the session manager.
        /// </summary>
        /// <returns>The instance of the session manager.</returns>
        public ISessionManager SessionManager => _sessionManager;

        /// <summary>
        /// Gets the socket manager.
        /// </summary>
        /// <returns>The instance of the socket manager.</returns>
        public ISocketManager SocketManager => _socketManager;

        /// <summary>
        /// Gets the theme manager.
        /// </summary>
        /// <returns>The instance of the theme manager.</returns>
        public IThemeManager ThemeManager => _themeManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        protected ComponentHub(IHttpServerContext httpServerContext)
        {
            _httpServerContext = httpServerContext;

            // order is relevant
            _pluginManager = CreateInstance(typeof(PluginManager)) as PluginManager
                ?? throw new InvalidOperationException("Failed to create PluginManager.");
            _packageManager = CreateInstance(typeof(PackageManager)) as PackageManager
                ?? throw new InvalidOperationException("Failed to create PackageManager.");
            _logManager = CreateInstance(typeof(LogManager)) as LogManager
                ?? throw new InvalidOperationException("Failed to create LogManager.");
            _internationalizationManager = CreateInstance(typeof(InternationalizationManager)) as InternationalizationManager
                ?? throw new InvalidOperationException("Failed to create InternationalizationManager.");
            _applicationManager = CreateInstance(typeof(ApplicationManager)) as ApplicationManager
                ?? throw new InvalidOperationException("Failed to create ApplicationManager.");
            _sitemapManager = CreateInstance(typeof(SitemapManager)) as SitemapManager
                ?? throw new InvalidOperationException("Failed to create SitemapManager.");
            _fragmentManager = CreateInstance(typeof(FragmentManager)) as FragmentManager
                ?? throw new InvalidOperationException("Failed to create FragmentManager.");
            _endpointManager = CreateInstance(typeof(EndpointManager)) as EndpointManager
                ?? throw new InvalidOperationException("Failed to create EndpointManager.");
            _assetManager = CreateInstance(typeof(AssetManager)) as AssetManager
                ?? throw new InvalidOperationException("Failed to create AssetManager.");
            _resourceManager = CreateInstance(typeof(ResourceManager)) as ResourceManager
                ?? throw new InvalidOperationException("Failed to create ResourceManager.");
            _includeManager = CreateInstance(typeof(IncludeManager)) as IncludeManager
                ?? throw new InvalidOperationException("Failed to create IncludeManager.");
            _pageManager = CreateInstance(typeof(PageManager)) as PageManager
                ?? throw new InvalidOperationException("Failed to create PageManager.");
            _settingPageManager = CreateInstance(typeof(SettingPageManager)) as SettingPageManager
                ?? throw new InvalidOperationException("Failed to create SettingPageManager.");
            _restApiManager = CreateInstance(typeof(RestApiManager)) as RestApiManager
                ?? throw new InvalidOperationException("Failed to create RestApiManager.");
            _statusPageManager = CreateInstance(typeof(StatusPageManager)) as StatusPageManager
                ?? throw new InvalidOperationException("Failed to create StatusPageManager.");
            _eventManager = CreateInstance(typeof(EventManager)) as EventManager
                ?? throw new InvalidOperationException("Failed to create EventManager.");
            _jobManager = CreateInstance(typeof(JobManager)) as JobManager
                ?? throw new InvalidOperationException("Failed to create JobManager.");
            _sessionManager = CreateInstance(typeof(SessionManager)) as SessionManager
                ?? throw new InvalidOperationException("Failed to create SessionManager.");
            _taskManager = CreateInstance(typeof(TaskManager)) as TaskManager
                ?? throw new InvalidOperationException("Failed to create TaskManager.");
            _identityProviderManager = CreateInstance(typeof(IdentityProviderManager)) as IdentityProviderManager
                ?? throw new InvalidOperationException("Failed to create IdentityProviderManager.");
            _identityManager = CreateInstance(typeof(IdentityManager)) as IdentityManager
                ?? throw new InvalidOperationException("Failed to create IdentityManager.");
            _socketManager = CreateInstance(typeof(SocketManager)) as SocketManager
                ?? throw new InvalidOperationException("Failed to create SocketManager.");
            _themeManager = CreateInstance(typeof(ThemeManager)) as ThemeManager
                ?? throw new InvalidOperationException("Failed to create ThemeManager.");

            _internationalizationManager.Register(typeof(HttpServer).Assembly, typeof(HttpServer).Assembly.GetName().Name?.ToLower());

            _httpServerContext?.Log?.Debug
            (
                _internationalizationManager?.Translate("webexpress.webcore:componentmanager.initialization")
            );

            _pluginManager?.AddPlugin += (sender, pluginContext) =>
            {
                Register(pluginContext);
            };

            PluginManager?.RemovePlugin += (sender, pluginContext) =>
            {
                Remove(pluginContext);
            };
        }

        /// <summary>
        /// Creates and initializes a component.
        /// </summary>
        /// <param name="componentType">The component class.</param>
        /// <returns>The instance of the create and initialized component.</returns>
        private IComponentManager CreateInstance(Type componentType)
        {
            if (componentType is null)
            {
                return null;
            }
            else if (!componentType.GetInterfaces().Any(x => x == typeof(IComponentManager)))
            {
                _httpServerContext?.Log?.Warning
                (
                    _internationalizationManager?.Translate
                    (
                        "webexpress.webcore:componentmanager.wrongtype",
                        componentType?.FullName, typeof(IComponentManager).FullName
                    )
                );

                return null;
            }

            try
            {
                return ComponentActivator.CreateInstance<IComponentManager>(componentType, _httpServerContext, this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Component creation failed: {componentType.FullName}");
                Console.WriteLine(ex.InnerException?.Message);
                _httpServerContext?.Log?.Exception(ex);
            }

            return null;
        }

        /// <summary>
        /// Returns a component based on its id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>The instance of the component or null.</returns>
        public IComponentManager GetComponentManager(string id)
        {
            return _dictionary.Values
                .SelectMany(x => x)
                .Where(x => x.ComponentId.Equals(id, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.ComponentInstance)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a component based on its type.
        /// </summary>
        /// <typeparam name="TComponentManager">The component class.</typeparam>
        /// <returns>The instance of the component or null.</returns>
        public TComponentManager GetComponentManager<TComponentManager>()
            where TComponentManager : IComponentManager
        {
            return (TComponentManager)_dictionary.Values
                .SelectMany(x => x)
                .Where(x => x.ComponentClass == typeof(TComponentManager))
                .Select(x => x.ComponentInstance)
                .FirstOrDefault();
        }

        /// <summary>
        /// Discovers and registers the components from the specified plugin.
        /// </summary>
        /// <param name="pluginContext">A plugin context that contain the components.</param>
        internal void Register(IPluginContext pluginContext)
        {
            // the plugin has already been registered
            if (_dictionary.ContainsKey(pluginContext))
            {
                return;
            }

            var assembly = pluginContext.Assembly;

            // initialize the component entry as an empty list for easier manipulation
            var componentList = new List<ComponentItem>();
            _dictionary.Add(pluginContext, componentList);

            foreach (var type in assembly
                .GetExportedTypes()
                .Where(x => x.IsClass && x.IsSealed && x.GetInterface(typeof(IComponentManager).Name) is not null))
            {
                var id = type.FullName?.ToLower();

                // determining attributes
                var componentInstance = CreateInstance(type);

                // check for duplicates
                if (!componentList.Any(x => x.ComponentId.Equals(id, StringComparison.OrdinalIgnoreCase)))
                {
                    componentList.Add(new ComponentItem()
                    {
                        ComponentClass = type,
                        ComponentId = id,
                        ComponentInstance = componentInstance
                    });

                    _httpServerContext?.Log?.Debug
                    (
                        _internationalizationManager.Translate("webexpress.webcore:componentmanager.register", id)
                    );

                    // raises the AddComponent event
                    OnAddComponent(componentInstance);
                }
                else
                {
                    _httpServerContext?.Log?.Warning
                    (
                        _internationalizationManager.Translate("webexpress.webcore:componentmanager.duplicate", id)
                    );
                }
            }

            // make sure the dictionary uses IEnumerable as value type, if the dictionary requires it
            _dictionary[pluginContext] = componentList;

            Log();
        }

        /// <summary>
        /// Boots the components.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        internal void BootComponent(IPluginContext pluginContext)
        {
            _pluginManager.Boot(pluginContext);
            _applicationManager.Boot(pluginContext);

            foreach (var component in _dictionary.Values
                .SelectMany(x => x)
                .Select(x => x.ComponentInstance)
                .OfType<IExecutableElements>())
            {
                component.Boot(pluginContext);
            }
        }

        /// <summary>
        /// Boots the components.
        /// </summary>
        /// <param name="pluginContexts">A enumeration of plugin contexts.</param>
        internal void BootComponent(IEnumerable<IPluginContext> pluginContexts)
        {
            foreach (var pluginContext in pluginContexts)
            {
                BootComponent(pluginContext);
            }
        }

        /// <summary>
        /// Starts the component.
        /// </summary>
        public void Execute()
        {
            _httpServerContext?.Log?.Debug
            (
                _internationalizationManager.Translate("webexpress.webcore:componentmanager.execute")
            );

            _packageManager.Execute();
            _jobManager.Execute();
        }

        /// <summary>
        /// Shutting down the component manager.
        /// </summary>
        public void ShutDown()
        {
            _httpServerContext?.Log?.Debug
            (
                _internationalizationManager.Translate("webexpress.webcore:componentmanager.shutdown")
            );
        }

        /// <summary>
        /// Shutting down the component.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        internal void ShutDownComponent(IPluginContext pluginContext)
        {
            _pluginManager.ShutDown(pluginContext);
            _applicationManager.ShutDown(pluginContext);

            foreach (var component in _dictionary.Values
                .SelectMany(x => x)
                .Select(x => x.ComponentInstance)
                .OfType<IExecutableElements>())
            {
                component.ShutDown(pluginContext);
            }
        }

        /// <summary>
        /// Removes all components associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the applications to remove.</param>
        public void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            // try to get a list for safe removal and iteration
            if (_dictionary.TryGetValue(pluginContext, out var componentItems))
            {
                // for IEnumerable, first eagerly materialize the enumeration
                var items = componentItems.ToList();
                if (items.Count == 0)
                {
                    return;
                }

                foreach (var componentItem in items)
                {
                    // raise the RemoveComponent event for each item
                    OnRemoveComponent(componentItem.ComponentInstance);

                    _httpServerContext?.Log?.Debug
                    (
                        _internationalizationManager.Translate("webexpress.webcore:componentmanager.remove")
                    );
                }
            }

            _dictionary.Remove(pluginContext);
        }

        /// <summary>
        /// Raises the AddComponent event.
        /// </summary>
        /// <param name="component">The component.</param>
        private void OnAddComponent(IComponentManager component)
        {
            AddComponent?.Invoke(null, component);
        }

        /// <summary>
        /// Raises the RemoveComponent event.
        /// </summary>
        /// <param name="component">The component.</param>
        private void OnRemoveComponent(IComponentManager component)
        {
            RemoveComponent?.Invoke(null, component);
        }

        /// <summary>
        /// Output of the components to the log.
        /// </summary>
        private void Log()
        {
            // materialize the (relatively expensive) managers enumeration once
            var managers = Managers.ToList();

            if (_lastCounter == managers.Count)
            {
                return;
            }

            using var frame = new LogFrameSimple(_httpServerContext?.Log);
            var output = new List<string>
            {
                _internationalizationManager.Translate("webexpress.webcore:componentmanager.component")
            };

            foreach (var manager in managers)
            {
                output.Add
                (
                   string.Empty.PadRight(2) +
                   _internationalizationManager.Translate("webexpress.webcore:componentmanager.name", manager?.GetType()?.Name.ToLower())
                );
            }

            _httpServerContext?.Log?.Info(string.Join(Environment.NewLine, output));
            _lastCounter = managers.Count;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
