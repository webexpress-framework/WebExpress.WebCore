using System;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebCertificate;
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
    /// Interface of the central management of manager components.
    /// </summary>
    public interface IComponentHub : IComponentManager
    {
        /// <summary>
        /// Gets the shared certificate service used by hosting and application components.
        /// </summary>
        ICertificateManager CertificateManager { get; }

        /// <summary>
        /// An event that fires when an component is added.
        /// </summary>
        event EventHandler<IComponentManager> AddComponent;

        /// <summary>
        /// An event that fires when an component is removed.
        /// </summary>
        event EventHandler<IComponentManager> RemoveComponent;

        /// <summary>
        /// Gets all registered components.
        /// </summary>
        IEnumerable<IComponentManager> Managers { get; }

        /// <summary>
        /// Gets the log manager.
        /// </summary>
        /// <returns>The instance of the log manager.</returns>
        ILogManager LogManager { get; }

        /// <summary>
        /// Gets the package manager.
        /// </summary>
        /// <returns>The instance of the package manager.</returns>
        IPackageManager PackageManager { get; }

        /// <summary>
        /// Gets the plugin manager.
        /// </summary>
        /// <returns>The instance of the plugin manager.</returns>
        public IPluginManager PluginManager { get; }

        /// <summary>
        /// Gets the application manager.
        /// </summary>
        /// <returns>The instance of the application manager.</returns>
        IApplicationManager ApplicationManager { get; }

        /// <summary>
        /// Gets the event manager.
        /// </summary>
        /// <returns>The instance of the event manager.</returns>
        IEventManager EventManager { get; }

        /// <summary>
        /// Gets the job manager.
        /// </summary>
        /// <returns>The instance of the job manager.</returns>
        IJobManager JobManager { get; }

        /// <summary>
        /// Gets the task manager.
        /// </summary>
        /// <returns>The instance of the task manager.</returns>
        ITaskManager TaskManager { get; }

        /// <summary>
        /// Gets the endpoint manager.
        /// </summary>
        /// <returns>The instance of the endpoint manager.</returns>
        IEndpointManager EndpointManager { get; }

        /// <summary>
        /// Gets the asset manager.
        /// </summary>
        /// <returns>The instance of the asset manager.</returns>
        public IAssetManager AssetManager { get; }

        /// <summary>
        /// Gets the resource manager.
        /// </summary>
        /// <returns>The instance of the resource manager.</returns>
        IResourceManager ResourceManager { get; }

        /// <summary>
        /// Gets the include manager.
        /// </summary>
        /// <returns>The instance of the include manager.</returns>
        IIncludeManager IncludeManager { get; }

        /// <summary>
        /// Gets the page manager.
        /// </summary>
        /// <returns>The instance of the page manager.</returns>
        IPageManager PageManager { get; }

        /// <summary>
        /// Gets the setting page manager.
        /// </summary>
        /// <returns>The instance of the setting page manager.</returns>
        ISettingPageManager SettingPageManager { get; }

        /// <summary>
        /// Gets the rest api manager.
        /// </summary>
        /// <returns>The instance of the rest api manager.</returns>
        IRestApiManager RestApiManager { get; }

        /// <summary>
        /// Gets the sitemap manager.
        /// </summary>
        /// <returns>The instance of the sitemap manager.</returns>
        ISitemapManager SitemapManager { get; }

        /// <summary>
        /// Gets the fragment manager.
        /// </summary>
        /// <returns>The instance of the fragment manager.</returns>
        IFragmentManager FragmentManager { get; }

        /// <summary>
        /// Gets the status page manager.
        /// </summary>
        /// <returns>The instance of the status page manager.</returns>
        IStatusPageManager StatusPageManager { get; }

        /// <summary>
        /// Gets the internationalization manager.
        /// </summary>
        /// <returns>The instance of the internationalization manager.</returns>
        IInternationalizationManager InternationalizationManager { get; }

        /// <summary>
        /// Gets the identity manager.
        /// </summary>
        /// <returns>The instance of the identity manager.</returns>
        IIdentityManager IdentityManager { get; }

        /// <summary>
        /// Provides application-scoped authentication sources.
        /// </summary>
        IIdentityProviderManager IdentityProviderManager { get; }

        /// <summary>
        /// Resolves the durable replay and revocation store bound to each application.
        /// </summary>
        IIdentityTokenStoreManager IdentityTokenStoreManager { get; }

        /// <summary>
        /// Gets the session manager.
        /// </summary>
        /// <returns>The instance of the session manager.</returns>
        ISessionManager SessionManager { get; }

        /// <summary>
        /// Gets the socket manager.
        /// </summary>
        /// <returns>The instance of the socket manager.</returns>
        ISocketManager SocketManager { get; }

        /// <summary>
        /// Gets the theme manager.
        /// </summary>
        /// <returns>The instance of the theme manager.</returns>
        IThemeManager ThemeManager { get; }

        /// <summary>
        /// Returns a component based on its id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>The instance of the component.</returns>
        IComponentManager GetComponentManager(string id);

        /// <summary>
        /// Returns a component based on its type.
        /// </summary>
        /// <typeparam name="TComponentManager">The component class.</typeparam>
        /// <returns>The instance of the component.</returns>
        TComponentManager GetComponentManager<TComponentManager>()
            where TComponentManager : IComponentManager;
    }
}
