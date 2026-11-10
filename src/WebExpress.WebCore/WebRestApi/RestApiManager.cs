using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebRestApi.Model;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebRestApi
{
    /// <summary>
    /// The rest api manager manages rest api resources, which can be called with a URI (Uniform page Identifier).
    /// </summary>
    public partial class RestApiManager : IRestApiManager, IDisposable
    {
        // synchronization guard for protecting _dictionary and related mutable state
        private readonly Lock _guard = new();

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        // instantiate the dictionary; assume RestApiDictionary is a non-thread-safe collection
        private readonly RestApiDictionary _dictionary = [];

        [GeneratedRegex(@"(?:_|[Vv])(\d+)_?")]
        private static partial Regex ApiVersionRegex();

        /// <summary>
        /// An event that fires when an rest api resource is added.
        /// </summary>
        public event EventHandler<IRestApiContext> AddRestApi;

        /// <summary>
        /// An event that fires when an rest api resource is removed.
        /// </summary>
        public event EventHandler<IRestApiContext> RemoveRestApi;

        /// <summary>
        /// Gets all rest api resource contexts.
        /// </summary>
        public IEnumerable<IRestApiContext> RestApis
        {
            get
            {
                // return a stable snapshot to avoid enumeration during concurrent modifications
                lock (_guard)
                {
                    return [.. _dictionary.Values
                        .SelectMany(x => x.Values)
                        .SelectMany(x => x.Values)
                        .Select(x => x.RestApiContext)];
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private RestApiManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            var endpointtRegistration = new EndpointRegistration()
            {
                EndpointResolver = (type, applicationContext) => applicationContext is not null
                    ? GetRestApi(type, applicationContext)
                    : GetRestApi(type),
                EndpointsResolver = () => RestApis,
                HandleRequest = (request, endpointContext) =>
                {
                    // get rest api context and create or obtain instance
                    var restApiContext = endpointContext as IRestApiContext;
                    var restApiItem = _dictionary.Values
                        .SelectMany(x => x.Values)
                        .SelectMany(x => x.Values)
                        .FirstOrDefault(x => x.RestApiContext.Equals(restApiContext));

                    var restApi = CreateApiInstance(restApiItem);

                    // if no resource found, return bad request
                    if (restApiContext is null || restApi is null)
                    {
                        return new ResponseBadRequest()
                        {
                            Content = I18N.Translate("webexpress.webcore:restapimanager.resourcenotfound")
                        };
                    }

                    // execute according to allowed methods
                    if (restApiContext.Methods.Any(x => x.Equals((RequestMethod)request.Method)))
                    {
                        switch (request.Method)
                        {
                            case RequestMethod.POST:
                                if (restApiItem.PostMethod is not null)
                                {
                                    return (Response)(restApiItem.PostMethod
                                        .Invoke(restApi, [request])
                                        ?? new ResponseOK());
                                }
                                break;
                            case RequestMethod.GET:
                                if (restApiItem.GetMethod is not null)
                                {
                                    return (Response)(restApiItem.GetMethod
                                        .Invoke(restApi, [request])
                                        ?? new ResponseOK());
                                }
                                break;
                            case RequestMethod.PATCH:
                                if (restApiItem.PatchMethod is not null)
                                {
                                    return (Response)(restApiItem.PatchMethod
                                        .Invoke(restApi, [request])
                                        ?? new ResponseOK());
                                }
                                break;
                            case RequestMethod.PUT:
                                if (restApiItem.PutMethod is not null)
                                {
                                    return (Response)(restApiItem.PutMethod
                                        .Invoke(restApi, [request])
                                        ?? new ResponseOK());
                                }
                                break;
                            case RequestMethod.DELETE:
                                if (restApiItem.DeleteMethod is not null)
                                {
                                    return (Response)(restApiItem.DeleteMethod
                                        .Invoke(restApi, [request])
                                        ?? new ResponseOK());
                                }
                                break;
                            default:
                                return new ResponseBadRequest()
                                {
                                    Content = I18N.Translate("webexpress.webcore:restapimanager.methodnotsupported", request.Method.ToString())
                                };
                        }
                    }

                    return new ResponseBadRequest()
                    {
                        Content = I18N.Translate("webexpress.webcore:restapimanager.methodnotsupported", request.Method.ToString())
                    };
                }
            };

            AddRestApi += (sender, e) => endpointtRegistration.AddEndpoint?.Invoke(sender, e);
            RemoveRestApi += (sender, e) => endpointtRegistration.RemoveEndpoint?.Invoke(sender, e);

            _componentHub?.EndpointManager.Register<RestApiContext>(endpointtRegistration);

            _httpServerContext?.Log?.Debug(I18N.Translate("webexpress.webcore:restapimanager.initialization"));
        }

        /// <summary>
        /// Returns an enumeration of all containing rest api contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose rest apis are to be registered.</param>
        /// <returns>An enumeration of rest api resource contexts.</returns>
        public IEnumerable<IRestApiContext> GetRestApi(IPluginContext pluginContext)
        {
            lock (_guard)
            {
                if (_dictionary.TryGetValue(pluginContext, out var pluginResources))
                {
                    // return snapshot list
                    return [.. pluginResources
                        .SelectMany(x => x.Value)
                        .Select(x => x.Value.RestApiContext)];
                }

                return [];
            }
        }

        /// <summary>
        /// Returns an enumeration of rest api resource contexts.
        /// </summary>
        /// <typeparam name="T">The rest api resource type.</typeparam>
        /// <returns>An enumeration of rest api resource contexts.</returns>
        public IEnumerable<IRestApiContext> GetRestApi<T>() where T : IRestApi
        {
            return GetRestApi(typeof(T));
        }

        /// <summary>
        /// Returns an enumeration of rest api resource contexts.
        /// </summary>
        /// <param name="restApiType">The rest api resource type.</param>
        /// <returns>An enumeration of rest api resource contexts.</returns>
        public IEnumerable<IRestApiContext> GetRestApi(Type restApiType)
        {
            lock (_guard)
            {
                return [.. _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.RestApiClass.Equals(restApiType))
                    .Select(x => x.RestApiContext)];
            }
        }

        /// <summary>
        /// Returns an enumeration of rest api resource contexts for a given application.
        /// </summary>
        /// <param name="restApiType">The rest api type.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of rest api resource contexts.</returns>
        public IEnumerable<IRestApiContext> GetRestApi(Type restApiType, IApplicationContext applicationContext)
        {
            lock (_guard)
            {
                return [.. _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.RestApiClass.Equals(restApiType))
                    .Where(x => x.RestApiContext.ApplicationContext.Equals(applicationContext))
                    .Select(x => x.RestApiContext)];
            }
        }

        /// <summary>
        /// Returns an enumeration of rest api resource contexts for a given application.
        /// </summary>
        /// <typeparam name="T">The rest api resource type.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of rest api resource contexts.</returns>
        public IEnumerable<IRestApiContext> GetRestApi<T>(IApplicationContext applicationContext) where T : IRestApi
        {
            lock (_guard)
            {
                return [.. _dictionary.Values
                     .SelectMany(x => x.Values)
                     .SelectMany(x => x.Values)
                     .Where(x => x.RestApiClass.Equals(typeof(T)))
                     .Where(x => x.RestApiContext.ApplicationContext.Equals(applicationContext))
                     .Select(x => x.RestApiContext)];
            }
        }

        /// <summary>
        /// Returns the rest api resource context.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="restApiId">The rest api resource id.</param>
        /// <returns>An rest api resource context or null.</returns>
        public IRestApiContext GetRestApi(IApplicationContext applicationContext, string restApiId)
        {
            lock (_guard)
            {
                return _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.RestApiContext.ApplicationContext.Equals(applicationContext))
                    .Where(x => x.RestApiContext.EndpointId.Equals(restApiId))
                    .Select(x => x.RestApiContext)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Returns the rest api resource context.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <param name="restApiId">The rest api resource id.</param>
        /// <returns>An rest api resource context or null.</returns>
        public IRestApiContext GetRestApi(string applicationId, string restApiId)
        {
            lock (_guard)
            {
                return _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.RestApiContext.ApplicationContext.ApplicationId.Equals(applicationId))
                    .Where(x => x.RestApiContext.EndpointId.Equals(restApiId))
                    .Select(x => x.RestApiContext)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Creates a new rest api resource and returns it. If a rest api resource already exists (through caching), the existing instance is returned.
        /// Thread-safe: cached instance creation and assignment is protected.
        /// </summary>
        /// <param name="apiItem">The item used for rest api resource creation.</param>
        /// <returns>The created or cached rest api resource.</returns>
        private IRestApi CreateApiInstance(RestApiItem apiItem)
        {
            if (apiItem is null)
            {
                return null;
            }

            // locate resourceItem inside lock to get a consistent view
            lock (_guard)
            {
                // if instance already cached, return immediately
                if (apiItem.Instance is not null)
                {
                    return apiItem.Instance;
                }

                // if caching is enabled, create and assign the instance under lock to avoid double-creation
                if (apiItem.Cache)
                {
                    // create instance while holding the lock to ensure only one creation and assignment occurs
                    var instanceCached = ComponentActivator.CreateInstance<IRestApi, IRestApiContext>
                    (
                        apiItem.RestApiClass,
                        apiItem.RestApiContext,
                        _httpServerContext,
                        _componentHub,
                        apiItem.ApplicationContext
                    );

                    apiItem.Instance = instanceCached;
                    return instanceCached;
                }
            }

            // if not caching, create instance outside lock (no shared state to modify)
            var instanceNoCache = ComponentActivator.CreateInstance<IRestApi, IRestApiContext>
            (
                apiItem.RestApiClass,
                apiItem.RestApiContext,
                _httpServerContext,
                _componentHub,
                apiItem.ApplicationContext
            );

            return instanceNoCache;
        }

        /// <summary>
        /// Discovers and binds rest apis to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose rest apis are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            lock (_guard)
            {
                if (_dictionary.ContainsKey(pluginContext))
                {
                    return;
                }

                Register(pluginContext, _componentHub.GetBoundApplications(pluginContext));
            }
        }

        /// <summary>
        /// Discovers and binds rest apis to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose rest apis are to be associated.</param>
        private void Register(IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub?.PluginManager?.GetPlugins(applicationContext))
            {
                lock (_guard)
                {
                    if (_dictionary.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext))
                    {
                        continue;
                    }
                }

                Register(pluginContext, new[] { applicationContext });
            }
        }

        /// <summary>
        /// Registers rest apis for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            // assembly and reflection operations are per-plugin and read-only; mutations to _dictionary are synchronized
            var assembly = pluginContext?.Assembly;

            foreach (var restApiType in assembly.GetTypes()
                .Where(x => x.IsClass == true && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(IRestApi).Name) is not null))
            {
                var id = restApiType.FullName?.ToLower();
                var segment = default(ISegmentAttribute);
                var title = restApiType.Name;
                var contextPath = string.Empty;
                var includeSubPaths = false;
                var conditions = new List<ICondition>();
                var cache = false;
                var match = ApiVersionRegex().Match(id);
                var versionSegment = match.Success ? match.Groups[0].Value.Replace(".", "") : "";
                var version = match.Success && uint.TryParse(match.Groups[1].Value, out var result) ? result : 1u;
                var policies = new List<IIdentityPolicy>();
                var attributes = restApiType.CustomAttributes
                    .Where(x => !x.AttributeType.GetInterfaces().Contains(typeof(IEndpointAttribute)) &&
                                !x.AttributeType.GetInterfaces().Contains(typeof(IPageAttribute)));
                var getMethod = restApiType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttributes(typeof(MethodAttribute), false)
                    .Cast<MethodAttribute>()
                    .Any(attr => attr.RequestMethod == RequestMethod.GET))
                    .FirstOrDefault();
                var postMethod = restApiType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttributes(typeof(MethodAttribute), false)
                    .Cast<MethodAttribute>()
                    .Any(attr => attr.RequestMethod == RequestMethod.POST))
                    .FirstOrDefault();
                var patchMethod = restApiType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttributes(typeof(MethodAttribute), false)
                    .Cast<MethodAttribute>()
                    .Any(attr => attr.RequestMethod == RequestMethod.PATCH))
                    .FirstOrDefault();
                var putMethod = restApiType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttributes(typeof(MethodAttribute), false)
                    .Cast<MethodAttribute>()
                    .Any(attr => attr.RequestMethod == RequestMethod.PUT))
                    .FirstOrDefault();
                var deleteMethod = restApiType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttributes(typeof(MethodAttribute), false)
                    .Cast<MethodAttribute>()
                    .Any(attr => attr.RequestMethod == RequestMethod.DELETE))
                    .FirstOrDefault();

                var methods = new[]
                {
                    (getMethod,    RequestMethod.GET),
                    (postMethod,   RequestMethod.POST),
                    (patchMethod,  RequestMethod.PATCH),
                    (putMethod,    RequestMethod.PUT),
                    (deleteMethod, RequestMethod.DELETE)
                }
                    .Where(x => x.Item1 is not null)
                    .Select(x => x.Item2);

                foreach
                (
                    var attribute in restApiType
                        .GetCustomAttributes(inherit: true)
                        .Where(x => x.GetType().GetInterfaces().Contains(typeof(IEndpointAttribute))))
                {
                    var attributeType = attribute.GetType();

                    // segment attribute
                    if (attributeType.GetInterfaces().Contains(typeof(ISegmentAttribute)))
                    {
                        segment = attribute as ISegmentAttribute;

                        continue;
                    }

                    // context path
                    if (attributeType == typeof(ContextPathAttribute))
                    {
                        contextPath = (attribute as ContextPathAttribute)?.ContextPath;

                        continue;
                    }

                    // include subpaths
                    if (attributeType == typeof(IncludeSubPathsAttribute))
                    {
                        includeSubPaths = (attribute as IncludeSubPathsAttribute)?.IncludeSubPaths
                            ?? false;

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

                // assign the rest api to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var prefix = EndpointManager.CreatePluginRoute(applicationContext, pluginContext);

                    var routePath = EndpointManager.CreateEndpointRoute
                    (
                        restApiType,
                        prefix,
                        segment,
                        [
                            new UriPathSegmentConstant("api"),
                            new UriPathSegmentVariableApiVersion<ParameterApiVersion>($"{version}")
                        ],
                        ["api", "restapi", "rest"]
                    ).RemoveSegment(versionSegment);

                    var restApiContext = new RestApiContext()
                    {
                        EndpointId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        Route = routePath,
                        Cache = cache,
                        Conditions = conditions,
                        IncludeSubPaths = includeSubPaths,
                        Attributes = EndpointManager.GetAttributeInstances(attributes),
                        Version = version,
                        Policies = policies,
                        Methods = methods.Distinct()
                    };

                    var restApiItem = new RestApiItem(_componentHub?.EndpointManager)
                    {
                        EndpointId = new ComponentId(restApiType.FullName),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        RestApiContext = restApiContext,
                        RestApiClass = restApiType,
                        Methods = methods.Distinct(),
                        Version = version,
                        Cache = cache,
                        Conditions = conditions,
                        IncludeSubPaths = includeSubPaths,
                        Attributes = attributes.Select(x => x.AttributeType),
                        GetMethod = getMethod,
                        PostMethod = postMethod,
                        PatchMethod = patchMethod,
                        PutMethod = putMethod,
                        DeleteMethod = deleteMethod
                    };

                    // add mutation protected by lock to avoid concurrent modifications
                    var added = false;
                    lock (_guard)
                    {
                        added = _dictionary.AddRestApiItem(pluginContext, applicationContext, restApiItem);
                    }

                    if (added)
                    {
                        OnAddRestApi(restApiItem.RestApiContext);

                        _httpServerContext?.Log?.Debug(
                            I18N.Translate(
                                "webexpress.webcore:restapimanager.addrestapi",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Removes all rest apis associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the rest api resources to remove.</param>
        public void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            lock (_guard)
            {
                if (_dictionary.TryGetValue(pluginContext, out var value))
                {
                    foreach (var resourceItem in value.Values.SelectMany(x => x.Values))
                    {
                        OnRemoveRestApi(resourceItem.RestApiContext);
                        resourceItem.Dispose();
                    }

                    _dictionary.Remove(pluginContext);
                }
            }
        }

        /// <summary>
        /// Removes all rest apis associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the resources to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

            lock (_guard)
            {
                foreach (var pluginDict in _dictionary.Values)
                {
                    foreach (var appDict in pluginDict.Where(x => x.Key == applicationContext).Select(x => x.Value))
                    {
                        foreach (var resourceItem in appDict.Values)
                        {
                            OnRemoveRestApi(resourceItem.RestApiContext);
                            resourceItem.Dispose();
                        }
                    }

                    // remove the application mapping from the plugin dictionary
                    pluginDict.Remove(applicationContext);
                }
            }
        }

        /// <summary>
        /// Raises the AddRestApi event.
        /// </summary>
        /// <param name="resourceContext">The rest api resource context.</param>
        private void OnAddRestApi(IRestApiContext resourceContext)
        {
            AddRestApi?.Invoke(this, resourceContext);
        }

        /// <summary>
        /// Raises the RemoveRestApi event.
        /// </summary>
        /// <param name="pageContext">The rest api resource context.</param>
        private void OnRemoveRestApi(IRestApiContext pageContext)
        {
            RemoveRestApi?.Invoke(this, pageContext);
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
        /// Raises the event when an application is removed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the application being removed.</param>
        private void OnRemoveApplication(object sender, IApplicationContext e)
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