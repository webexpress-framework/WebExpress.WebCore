using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebPage.Model;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebScope;

namespace WebExpress.WebCore.WebPage
{
    /// <summary>
    /// The page manager manages page elements, which can be called with a URI (Uniform page Identifier).
    /// </summary>
    public class PageManager : IPageManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly PageDictionary _dictionary = new();
        private static readonly ConcurrentDictionary<Type, Delegate> _delegateCache = new();

        /// <summary>
        /// An event that fires when an page is added.
        /// </summary>
        public event EventHandler<IPageContext> AddPage;

        /// <summary>
        /// An event that fires when an page is removed.
        /// </summary>
        public event EventHandler<IPageContext> RemovePage;

        /// <summary>
        /// Gets all page contexts.
        /// </summary>
        public IEnumerable<IPageContext> Pages => _dictionary.All;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private PageManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            var endpointtRegistration = new EndpointRegistration()
            {
                EndpointResolver = (type, applicationContext) => applicationContext is not null
                    ? GetPages(type, applicationContext)
                    : GetPages(type),
                EndpointsResolver = () => Pages,
                HandleRequest = (request, endpontContext) =>
                {
                    // create or get page instance for this request
                    var pageInstance = CreatePageInstance(endpontContext as IPageContext);
                    var pageType = pageInstance.GetType();
                    var pageContext = endpontContext as IPageContext;
                    var renderContext = new RenderContext(pageInstance, pageContext, request);
                    var visualTreeContext = new VisualTreeContext(renderContext);

                    // determine visual tree type implemented by the page
                    var pageInterface = pageType.GetInterface(typeof(IPage<>).Name) ?? throw new InvalidOperationException($"Page type {pageType.FullName} does not implement IPage<>.");
                    var visualTreeType = pageInterface.GetGenericArguments()[0];

                    // obtain or create a cached open-instance delegate safely
                    if (!_delegateCache.TryGetValue(pageType, out var del))
                    {
                        // create an open-instance delegate: (instance, renderContext, visualTree) => instance.Process(renderContext, visualTree)
                        var instanceParam = Expression.Parameter(pageType, "instance");
                        var renderContextParam = Expression.Parameter(typeof(IRenderContext), "renderContext");
                        var visualTreeParam = Expression.Parameter(visualTreeType, "visualTree");

                        // find Process method matching signature Process(IRenderContext, TVisualTree)
                        var processMethod = pageType.GetMethod("Process", new[] { typeof(IRenderContext), visualTreeType }) ?? throw new InvalidOperationException($"Process method not found on type {pageType.FullName}");

                        // call instance.Process(renderContext, visualTree)
                        var callProcess = Expression.Call(instanceParam, processMethod, renderContextParam, visualTreeParam);

                        // compile lambda with signature (instance, renderContext, visualTree)
                        var lambda = Expression.Lambda(callProcess, instanceParam, renderContextParam, visualTreeParam).Compile();

                        // add to concurrent dictionary atomically; if another thread added concurrently, use the existing one
                        del = _delegateCache.GetOrAdd(pageType, lambda);
                    }

                    // create visual tree instance
                    IVisualTree visualTreeInstance = null;
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    var constructors = visualTreeType?.GetConstructors(flags);

                    if (constructors is not null)
                    {
                        // try constructors ordered by parameter count (descending)
                        foreach (var constructor in constructors.OrderByDescending(x => x.GetParameters().Length))
                        {
                            // injection
                            var parameters = constructor.GetParameters();
                            var hubProperties = _componentHub?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                            var contextIdProperty = pageContext.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(x => x.PropertyType == typeof(IComponentId))
                                .FirstOrDefault();

                            var parameterValues = parameters.Select(parameter =>
                                parameter.ParameterType == typeof(IComponentHub) ? _componentHub :
                                parameter.ParameterType == typeof(IHttpServerContext) ? _httpServerContext :
                                parameter.ParameterType == typeof(IPageContext) ? pageContext :
                                parameter.ParameterType == typeof(IApplicationContext) ? pageContext?.ApplicationContext :
                                parameter.ParameterType == typeof(IComponentId) ? contextIdProperty?.GetValue(pageContext) :
                                hubProperties.Where(x => x.PropertyType == parameter.ParameterType)
                                    .FirstOrDefault()?
                                    .GetValue(_componentHub) ?? null
                            ).ToArray();

                            // attempt to invoke constructor with resolved parameters
                            var invoked = constructor.Invoke(parameterValues);
                            if (invoked is IVisualTree visualTree)
                            {
                                visualTreeInstance = visualTree;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // fallback: try parameterless creation
                        visualTreeInstance = Activator.CreateInstance(visualTreeType) as IVisualTree;
                    }

                    if (visualTreeInstance is null)
                    {
                        throw new InvalidOperationException($"Could not create visual tree instance of type {visualTreeType.FullName} for page {pageType.FullName}.");
                    }

                    // execute the cached open-instance delegate; pass the current pageInstance
                    del.DynamicInvoke(pageInstance, renderContext, visualTreeInstance);

                    // build response from visual tree
                    var response = visualTreeInstance.GetResponse(visualTreeContext);

                    return response;
                }
            };

            AddPage += (sender, e) => endpointtRegistration.AddEndpoint?.Invoke(sender, e);
            RemovePage += (sender, e) => endpointtRegistration.RemoveEndpoint?.Invoke(sender, e);

            _componentHub?.EndpointManager.Register<PageContext>(endpointtRegistration);

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:pagemanager.initialization")
            );
        }

        /// <summary>
        /// Returns an enumeration of all containing page contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose pages are to be registered.</param>
        /// <returns>An enumeration of page contexts.</returns>
        public IEnumerable<IPageContext> GetPages(IPluginContext pluginContext)
        {
            return _dictionary.GetPages(pluginContext);
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <typeparam name="T">The page type.</typeparam>
        /// <returns>An enumeration of page contextes.</returns>
        public IEnumerable<IPageContext> GetPages<T>() where T : IPage
        {
            return GetPages(typeof(T));
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <param name="pageType">The page type.</param>
        /// <returns>An enumeration of page contextes.</returns>
        public IEnumerable<IPageContext> GetPages(Type pageType)
        {
            return _dictionary.GetPages(pageType);
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <param name="pageType">The page type.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of page contextes.</returns>
        public IEnumerable<IPageContext> GetPages(Type pageType, IApplicationContext applicationContext)
        {
            return _dictionary.GetPages(pageType, applicationContext);
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <typeparam name="T">The page type.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of page contextes.</returns>
        public IEnumerable<IPageContext> GetPages<T>(IApplicationContext applicationContext) where T : IPage
        {
            return _dictionary.GetPages<T>(applicationContext);
        }

        /// <summary>
        /// Returns the page context.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="pageId">The page id.</param>
        /// <returns>An page context or null.</returns>
        public IPageContext GetPage(IApplicationContext applicationContext, string pageId)
        {
            return _dictionary.GetPage(applicationContext, pageId);
        }

        /// <summary>
        /// Returns the page context.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <param name="pageId">The page id.</param>
        /// <returns>An page context or null.</returns>
        public IPageContext GetPage(string applicationId, string pageId)
        {
            return _dictionary.GetPage(applicationId, pageId);
        }

        /// <summary>
        /// Creates a new page and returns it. If a page already exists (through caching), the existing instance is returned.
        /// </summary>
        /// <param name="pageContext">The context used for page creation.</param>
        /// <returns>The created or cached page.</returns>
        private IEndpoint CreatePageInstance(IPageContext pageContext)
        {
            var resourceItem = _dictionary.GetPageItem(pageContext);

            if (resourceItem is not null && resourceItem.Instance is null)
            {
                var instance = ComponentActivator.CreateInstance<IEndpoint, IPageContext>
                (
                    resourceItem.PageClass,
                    pageContext,
                    _httpServerContext,
                    _componentHub,
                    pageContext.ApplicationContext
                );

                if (resourceItem.Cache)
                {
                    resourceItem.Instance = instance;
                }

                return instance;
            }

            return resourceItem?.Instance;
        }

        /// <summary>
        /// Discovers and binds pages to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose pages are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (_dictionary.Contains(pluginContext))
            {
                return;
            }

            Register(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
        }

        /// <summary>
        /// Discovers and binds pages to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose pages are to be associated.</param>
        private void Register(IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub?.PluginManager?.GetPlugins(applicationContext))
            {
                if (_dictionary.Contains(pluginContext, applicationContext))
                {
                    continue;
                }

                Register(pluginContext, [applicationContext]);
            }
        }

        /// <summary>
        /// Registers pages for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var assembly = pluginContext?.Assembly;

            foreach (var pageType in assembly.GetTypes()
                .Where(x => x.IsClass == true && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(IPage<>).Name) is not null))
            {
                var id = pageType.FullName?.ToLower();
                var segment = default(ISegmentAttribute);
                var icon = default(Type);
                var title = pageType.Name;
                var includeSubPaths = false;
                var scopes = new List<Type>();
                var conditions = new List<ICondition>();
                var cache = false;
                var domains = new List<Type>();
                var policies = new List<IIdentityPolicy>();
                var attributes = pageType.CustomAttributes
                    .Where(x => !x.AttributeType.GetInterfaces().Contains(typeof(IEndpointAttribute)) &&
                    !x.AttributeType.GetInterfaces().Contains(typeof(IPageAttribute)));

                foreach
                (
                    var attribute in pageType
                        .GetCustomAttributes(inherit: true)
                        .Where(x => x.GetType().GetInterfaces().Contains(typeof(IEndpointAttribute)))
                )
                {
                    var attributeType = attribute.GetType();

                    // segment attribute
                    if (attributeType.GetInterfaces().Contains(typeof(ISegmentAttribute)))
                    {
                        segment = attribute as ISegmentAttribute;
                        continue;
                    }

                    // include subpaths
                    if (attributeType == typeof(IncludeSubPathsAttribute))
                    {
                        includeSubPaths = (attribute as IncludeSubPathsAttribute)?.IncludeSubPaths ?? false;
                        continue;
                    }

                    // condition attribute (generic)
                    if (attributeType.IsGenericType
                        && attributeType.GetGenericTypeDefinition().Name == typeof(ConditionAttribute<>).Name
                        && attributeType.Namespace == typeof(ConditionAttribute<>).Namespace)
                    {
                        var conditionType = attributeType.GetGenericArguments().FirstOrDefault();
                        if (conditionType is not null)
                        {
                            conditions.Add(Activator.CreateInstance(conditionType) as ICondition);
                        }
                        continue;
                    }

                    // policy attribute (generic)
                    if (attributeType.IsGenericType
                        && attributeType.GetGenericTypeDefinition().Name == typeof(PolicyAttribute<>).Name
                        && attributeType.Namespace == typeof(PolicyAttribute<>).Namespace)
                    {
                        var policyType = attributeType.GetGenericArguments().FirstOrDefault();
                        if (policyType is not null)
                        {
                            policies.Add(Activator.CreateInstance(policyType) as IIdentityPolicy);
                        }
                        continue;
                    }

                    // cache attribute
                    if (attributeType == typeof(CacheAttribute))
                    {
                        cache = true;
                        continue;
                    }
                }

                foreach
                (
                    var attribute in pageType
                        .GetCustomAttributes(inherit: true)
                        .Where(x => x.GetType().GetInterfaces().Contains(typeof(IPageAttribute)))
                )
                {
                    var attributeType = attribute.GetType();

                    // web icon attribute (generic)
                    if (attributeType.IsGenericType &&
                        attributeType.GetGenericTypeDefinition() == typeof(WebIconAttribute<>))
                    {
                        icon = attributeType.GetGenericArguments().FirstOrDefault();
                        continue;
                    }

                    // title attribute
                    if (attributeType == typeof(TitleAttribute))
                    {
                        title = (attribute as TitleAttribute)?.Title;
                        continue;
                    }

                    // scope attribute (generic)
                    if (attributeType.IsGenericType &&
                        attributeType.GetGenericTypeDefinition().Name == typeof(ScopeAttribute<>).Name &&
                        attributeType.Namespace == typeof(ScopeAttribute<>).Namespace)
                    {
                        var scopeType = attributeType.GetGenericArguments().FirstOrDefault();
                        if (scopeType is not null)
                        {
                            scopes.Add(scopeType);
                        }
                        continue;
                    }

                    // domain attribute (generic)
                    if (attributeType.IsGenericType &&
                        attributeType.GetGenericTypeDefinition().Name == typeof(DomainAttribute<>).Name &&
                        attributeType.Namespace == typeof(DomainAttribute<>).Namespace)
                    {
                        var domainType = attributeType.GetGenericArguments().FirstOrDefault();
                        if (domainType is not null)
                        {
                            domains.Add(domainType);
                        }
                        continue;
                    }
                }

                if (pageType.GetInterfaces().Where(x => x == typeof(IScope)).Any())
                {
                    scopes.Add(pageType);
                }

                // assign the page to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var prefix = applicationContext.Route.Concat
                    (
                        applicationContext.PluginContext != pluginContext
                            ? pluginContext.PluginName.ToLower()
                            : ""
                    );
                    var routePath = EndpointManager.CreateEndpointRoute(pageType, prefix, segment);
                    var pageContext = new PageContext()
                    {
                        EndpointId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        PageTitle = title,
                        PageIcon = GetIcon(icon),
                        Route = routePath,
                        Scopes = scopes,
                        Domains = domains,
                        Cache = cache,
                        Conditions = conditions,
                        Policies = policies,
                        IncludeSubPaths = includeSubPaths,
                        Attributes = EndpointManager.GetAttributeInstances(attributes)
                    };

                    var pageItem = new PageItem(_componentHub?.EndpointManager)
                    {
                        EndpointId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        PageContext = pageContext,
                        Title = title,
                        PageClass = pageType,
                        Scopes = scopes,
                        Cache = cache,
                        Conditions = conditions,
                        IncludeSubPaths = includeSubPaths,
                        Attributes = attributes.Select(x => x.AttributeType)
                    };

                    if (_dictionary.AddPageItem(pluginContext, applicationContext, pageItem))
                    {
                        OnAddPage(pageItem.PageContext);

                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:pagemanager.addpage",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Removes all pages associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the pages to remove.</param>
        public void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            // the plugin has not been registered in the manager
            foreach (var pageContext in _dictionary.RemovePage(pluginContext))
            {
                OnRemovePage(pageContext);
            }
        }

        /// <summary>
        /// Removes all pages associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the page to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

            foreach (var pageContext in _dictionary.RemovePage(applicationContext))
            {
                OnRemovePage(pageContext);
            }
        }

        /// <summary>
        /// Raises the AddPage event.
        /// </summary>
        /// <param name="resourceContext">The page context.</param>
        private void OnAddPage(IPageContext resourceContext)
        {
            AddPage?.Invoke(this, resourceContext);
        }

        /// <summary>
        /// Raises the RemovePage event.
        /// </summary>
        /// <param name="pageContext">The page context.</param>
        private void OnRemovePage(IPageContext pageContext)
        {
            RemovePage?.Invoke(this, pageContext);
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
        /// Creates an instance of an icon of the specified type.
        /// </summary>
        /// <param name="iconType">
        /// The type of the icon to instantiate. Must implement the IIcon interface.
        /// </param>
        /// <returns>
        /// An instance of IIcon created from the specified type. Returns null if the icon cannot be instantiated.
        /// </returns>
        private static IIcon GetIcon(Type iconType)
        {
            return iconType is not null
                ? Activator.CreateInstance(iconType) as IIcon
                : null;
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

            GC.SuppressFinalize(this);
        }
    }
}
