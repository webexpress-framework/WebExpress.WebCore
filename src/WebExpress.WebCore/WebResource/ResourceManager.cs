using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebResource.Model;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebResource
{
    /// <summary>
    /// The resource manager manages WebExpress elements, which can be called with a 
    /// URI (Uniform Resource Identifier).
    /// </summary>
    public sealed class ResourceManager : IResourceManager, ISystemComponent
    {
        // synchronization root for protecting _dictionary and related mutable state
        private readonly Lock _guard = new();

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        // instantiate the dictionary; assume ResourceDictionary is a non-thread-safe collection
        private readonly ResourceDictionary _dictionary = [];

        /// <summary>
        /// An event that fires when an resource is added.
        /// </summary>
        public event EventHandler<IResourceContext> AddResource;

        /// <summary>
        /// An event that fires when an resource is removed.
        /// </summary>
        public event EventHandler<IResourceContext> RemoveResource;

        /// <summary>
        /// Gets all resource contexts.
        /// </summary>
        public IEnumerable<IResourceContext> Resources
        {
            get
            {
                // return a snapshot to avoid enumeration during concurrent modifications
                lock (_guard)
                {
                    return _dictionary.Values
                        .SelectMany(x => x.Values)
                        .SelectMany(x => x.Values)
                        .Select(x => x.ResourceContext)
                        .ToList();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ResourceManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            var endpointtRegistration = new EndpointRegistration()
            {
                EndpointResolver = (type, applicationContext) =>
                {
                    // return appropriate endpoints based on applicationContext
                    return applicationContext is not null ? GetResources(type, applicationContext) : GetResources(type);
                },
                EndpointsResolver = () =>
                {
                    // return snapshot of available resources
                    return Resources;
                },
                HandleRequest = (request, endpointContext) =>
                {
                    // create or obtain resource instance and process request
                    var resourceContext = endpointContext as IResourceContext;
                    var resource = CreateResourceInstance(resourceContext);

                    if (resource is null)
                    {
                        // resource not found, return status page or bad request
                        return new ResponseBadRequest()
                        {
                            Content = I18N.Translate("webexpress.webcore:resourcemanager.resourcenotfound")
                        };
                    }

                    return resource.Process(request);
                }
            };

            AddResource += (sender, e) =>
            {
                endpointtRegistration.AddEndpoint?.Invoke(sender, e);
            };
            RemoveResource += (sender, e) =>
            {
                endpointtRegistration.RemoveEndpoint?.Invoke(sender, e);
            };

            _componentHub?.EndpointManager.Register<ResourceContext>(endpointtRegistration);

            _httpServerContext?.Log?.Debug(
                I18N.Translate("webexpress.webcore:resourcemanager.initialization")
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
                if (_dictionary.ContainsKey(pluginContext))
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
                bool shouldContinue = false;

                lock (_guard)
                {
                    if (_dictionary.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext))
                    {
                        shouldContinue = true;
                    }
                }

                if (shouldContinue)
                {
                    continue;
                }

                Register(pluginContext, new[] { applicationContext });
            }
        }

        /// <summary>
        /// Registers resources for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            // assembly and reflection operations are per-plugin and read-only; mutations to _dictionary are synchronized
            var assembly = pluginContext?.Assembly;

            foreach (var resourceType in assembly.GetTypes()
                .Where(x => x.IsClass && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(IResource).Name) is not null)
                .Where(x => x.GetInterface(typeof(IStatusPage).Name) is null))
            {
                var id = resourceType.FullName?.ToLower();
                var segment = default(ISegmentAttribute);
                var contextPath = string.Empty;
                var includeSubPaths = false;
                var conditions = new List<ICondition>();
                var cache = false;
                var policies = new List<IIdentityPolicy>();
                var attributes = resourceType.CustomAttributes
                    .Where(x => !x.AttributeType.GetInterfaces().Contains(typeof(IEndpointAttribute)) &&
                                !x.AttributeType.GetInterfaces().Contains(typeof(IPageAttribute)));

                foreach
                (
                    var attribute in resourceType
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

                // assign the resource to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var prefix = applicationContext.Route.Concat(
                        applicationContext.PluginContext != pluginContext
                            ? pluginContext.PluginName.ToLower()
                            : ""
                    );

                    var routePath = EndpointManager.CreateEndpointRoute(resourceType, prefix, segment);
                    var resourceContext = new ResourceContext()
                    {
                        EndpointId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        Route = routePath,
                        Cache = cache,
                        Conditions = conditions,
                        IncludeSubPaths = includeSubPaths,
                        Policies = policies,
                        Attributes = EndpointManager.GetAttributeInstances(attributes)
                    };

                    var resourceItem = new ResourceItem(_componentHub?.ResourceManager)
                    {
                        EndpointId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        ResourceContext = resourceContext,
                        ResourceClass = resourceType,
                        Cache = cache,
                        Conditions = conditions,
                        IncludeSubPaths = includeSubPaths,
                        Attributes = attributes.Select(x => x.AttributeType)
                    };

                    bool added = false;

                    lock (_guard)
                    {
                        added = _dictionary.AddResourceItem(pluginContext, applicationContext, resourceItem);
                    }

                    if (added)
                    {
                        OnAddResource(resourceItem.ResourceContext);

                        _httpServerContext?.Log?.Debug(
                            I18N.Translate(
                                "webexpress.webcore:resourcemanager.addresource",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Removes all resources associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the resources to remove.</param>
        internal void Remove(IPluginContext pluginContext)
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
                        OnRemoveResource(resourceItem.ResourceContext);
                        resourceItem.Dispose();
                    }

                    _dictionary.Remove(pluginContext);
                }
            }
        }

        /// <summary>
        /// Removes all resources associated with the specified application context.
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
                            OnRemoveResource(resourceItem.ResourceContext);
                            resourceItem.Dispose();
                        }
                    }

                    // remove the application mapping from the plugin dictionary
                    pluginDict.Remove(applicationContext);
                }
            }
        }

        /// <summary>
        /// Returns an enumeration of all containing resource contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose resources are to be registered.</param>
        /// <returns>An enumeration of resource contexts.</returns>
        public IEnumerable<IResourceContext> GetResources(IPluginContext pluginContext)
        {
            lock (_guard)
            {
                if (_dictionary.TryGetValue(pluginContext, out var pluginResources))
                {
                    return pluginResources
                        .SelectMany(x => x.Value)
                        .Select(x => x.Value.ResourceContext)
                        .ToList();
                }

                return Enumerable.Empty<IResourceContext>();
            }
        }

        /// <summary>
        /// Returns an enumeration of resource contexts.
        /// </summary>
        /// <typeparam name="T">The resource type.</typeparam>
        /// <returns>An enumeration of resource contexts.</returns>
        public IEnumerable<IResourceContext> GetResources<T>() where T : IResource
        {
            return GetResources(typeof(T));
        }

        /// <summary>
        /// Returns an enumeration of resource contexts.
        /// </summary>
        /// <param name="resourceType">The resource type.</param>
        /// <returns>An enumeration of resource contexts.</returns>
        public IEnumerable<IResourceContext> GetResources(Type resourceType)
        {
            lock (_guard)
            {
                return _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.ResourceClass.Equals(resourceType))
                    .Select(x => x.ResourceContext)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns an enumeration of resource contexts.
        /// </summary>
        /// <param name="resourceType">The resource type.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of resource contexts.</returns>
        public IEnumerable<IResourceContext> GetResources(Type resourceType, IApplicationContext applicationContext)
        {
            lock (_guard)
            {
                return _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.ResourceClass.Equals(resourceType))
                    .Where(x => x.ResourceContext.ApplicationContext.Equals(applicationContext))
                    .Select(x => x.ResourceContext)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns an enumeration of resource contexts.
        /// </summary>
        /// <typeparam name="T">The resource type.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of resource contexts.</returns>
        public IEnumerable<IResourceContext> GetResources<T>(IApplicationContext applicationContext) where T : IResource
        {
            lock (_guard)
            {
                return _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.ResourceClass.Equals(typeof(T)))
                    .Where(x => x.ResourceContext.ApplicationContext.Equals(applicationContext))
                    .Select(x => x.ResourceContext)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns the resource context.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="resourceId">The resource id.</param>
        /// <returns>An resource context or null.</returns>
        public IResourceContext GetResource(IApplicationContext applicationContext, string resourceId)
        {
            lock (_guard)
            {
                return _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.ResourceContext.ApplicationContext.Equals(applicationContext))
                    .Where(x => x.ResourceContext.EndpointId.Equals(resourceId))
                    .Select(x => x.ResourceContext)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Returns the resource context.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <param name="resourceId">The resource id.</param>
        /// <returns>An resource context or null.</returns>
        public IResourceContext GetResource(string applicationId, string resourceId)
        {
            lock (_guard)
            {
                return _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => x.ResourceContext.ApplicationContext.ApplicationId.Equals(applicationId))
                    .Where(x => x.ResourceContext.EndpointId.Equals(resourceId))
                    .Select(x => x.ResourceContext)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Creates a new resource and returns it. If a resource already exists (through caching), the existing instance is returned.
        /// Thread-safe: cached instance creation and assignment is protected.
        /// </summary>
        /// <param name="resourceContext">The context used for resource creation.</param>
        /// <returns>The created or cached resource.</returns>
        private IResource CreateResourceInstance(IResourceContext resourceContext)
        {
            if (resourceContext is null)
            {
                return null;
            }

            ResourceItem resourceItem = null;

            // locate resourceItem and handle caching under lock
            lock (_guard)
            {
                resourceItem = _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .FirstOrDefault(x => x.ResourceContext.Equals(resourceContext));

                if (resourceItem is null)
                {
                    return null;
                }

                // if instance already cached, return immediately
                if (resourceItem.Instance is not null)
                {
                    return resourceItem.Instance as IResource;
                }

                // if caching is enabled, create and assign instance while holding lock to avoid double-creation
                if (resourceItem.Cache)
                {
                    var instanceCached = ComponentActivator.CreateInstance<IResource, IResourceContext>(
                        resourceItem.ResourceClass,
                        resourceContext,
                        _httpServerContext,
                        _componentHub,
                        resourceContext.ApplicationContext
                    );

                    resourceItem.Instance = instanceCached;
                    return instanceCached;
                }
            }

            // if not caching, create instance outside lock (no shared state to modify)
            var instanceNoCache = ComponentActivator.CreateInstance<IResource, IResourceContext>(
                resourceItem.ResourceClass,
                resourceContext,
                _httpServerContext,
                _componentHub,
                resourceContext.ApplicationContext
            );

            return instanceNoCache;
        }

        /// <summary>
        /// Raises the AddResource event.
        /// </summary>
        /// <param name="resourceContext">The resource context.</param>
        private void OnAddResource(IResourceContext resourceContext)
        {
            AddResource?.Invoke(this, resourceContext);
        }

        /// <summary>
        /// Raises the RemoveResource event.
        /// </summary>
        /// <param name="resourceContext">The resource context.</param>
        private void OnRemoveResource(IResourceContext resourceContext)
        {
            RemoveResource?.Invoke(this, resourceContext);
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