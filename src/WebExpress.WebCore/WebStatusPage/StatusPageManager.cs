using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebScope;
using WebExpress.WebCore.WebStatusPage.Model;

namespace WebExpress.WebCore.WebStatusPage
{
    /// <summary>
    /// Management of status pages.
    /// </summary>
    public class StatusPageManager : IStatusPageManager, ISystemComponent
    {
        // synchronization guard for protecting _dictionary and related mutable state
        private readonly Lock _guard = new();

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly StatusPageDictionary _dictionary = new StatusPageDictionary();

        // use a concurrent dictionary for defaults to make default lookups/updates thread-safe
        private readonly ConcurrentDictionary<int, StatusPageItem> _defaults = new();

        // change delegate cache to concurrent dictionary (open-instance delegates are cached)
        private static readonly ConcurrentDictionary<Type, Delegate> _delegateCache = new();

        /// <summary>
        /// An event that fires when an status page is added.
        /// </summary>
        public event EventHandler<IStatusPageContext> AddStatusPage;

        /// <summary>
        /// An event that fires when an status page is removed.
        /// </summary>
        public event EventHandler<IStatusPageContext> RemoveStatusPage;

        /// <summary>
        /// Gets all status pages.
        /// </summary>
        public IEnumerable<IStatusPageContext> StatusPages
        {
            get
            {
                // return a stable snapshot to avoid enumeration during concurrent modifications
                lock (_guard)
                {
                    return _dictionary.Values
                        .SelectMany(x => x.Values)
                        .SelectMany(x => x.Values)
                        .Select(x => x.StatusPageContext)
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
        private StatusPageManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:statuspagemanager.initialization")
            );
        }

        /// <summary>
        /// Discovers and binds status pages to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose status pages are to be associated.</param>
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
        /// Discovers and binds status pages to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose status pages are to be associated.</param>
        private void Register(IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub?.PluginManager?.GetPlugins(applicationContext))
            {
                bool already;
                lock (_guard)
                {
                    already = _dictionary.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext);
                }

                if (already)
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
            var assembly = pluginContext?.Assembly;

            foreach (var resource in assembly.GetTypes()
                .Where(x => x.IsClass == true && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(IStatusPage<>).Name) is not null))
            {
                var id = new ComponentId(resource.FullName);
                var statusResponse = typeof(ResponseInternalServerError);
                var icon = string.Empty;
                var title = resource.Name;
                var description = string.Empty;
                var defaultItem = false;

                foreach (var customAttribute in resource.CustomAttributes
                    .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(IStatusPageAttribute))))
                {
                    if (customAttribute.AttributeType.Name == typeof(StatusResponseAttribute<>).Name && customAttribute.AttributeType.Namespace == typeof(StatusResponseAttribute<>).Namespace)
                    {
                        statusResponse = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();
                    }
                    else if (customAttribute.AttributeType == typeof(TitleAttribute))
                    {
                        title = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(IconAttribute))
                    {
                        icon = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DescriptionAttribute))
                    {
                        description = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DefaultAttribute))
                    {
                        defaultItem = true;
                    }
                }

                // assign the status pages to existing applications.
                foreach (var applicationContext in applicationContexts)
                {
                    // validate that a StatusCodeAttribute is present; if not, log and skip registration for this resource
                    var statusCodeAttr = statusResponse?.GetCustomAttribute<StatusCodeAttribute>();
                    if (statusCodeAttr is null || statusCodeAttr.StatusCode == 0)
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:statuspagemanager.statuscodeless",
                                resource.Name,
                                applicationContext?.ApplicationId
                            )
                        );

                        // skip registration because no valid status code is available
                        continue;
                    }

                    var stausIcon = !string.IsNullOrEmpty(icon) ? RouteEndpoint.Combine(applicationContext.Route, icon) : null;
                    var statusCode = statusCodeAttr.StatusCode;
                    var statusPageContext = new StatusPageContext()
                    {
                        StatusPageId = id,
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        StatusCode = statusCode,
                        StatusTitle = title,
                        StatusIcon = stausIcon,
                        StatusDescription = description
                    };

                    var added = false;
                    // add into _dictionary under lock to avoid concurrent modifications
                    lock (_guard)
                    {
                        added = _dictionary.AddStatusPageItem(pluginContext, applicationContext, statusCode, new StatusPageItem()
                        {
                            StatusPageContext = statusPageContext,
                            PluginContext = pluginContext,
                            ApplicationContext = applicationContext,
                            StatusResponse = statusResponse,
                            StatusPageClass = resource
                        });
                    }

                    if (added)
                    {
                        OnAddStatusPage(statusPageContext);

                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:statuspagemanager.register",
                                statusResponse,
                                resource.Name
                            )
                        );
                    }
                    else
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:statuspagemanager.duplicat",
                                statusResponse,
                                resource.Name
                            )
                        );
                    }

                    // default handling using concurrent dictionary for atomicity
                    if (!_defaults.TryAdd(statusCode, new StatusPageItem()
                    {
                        StatusPageContext = new StatusPageContext()
                        {
                            StatusPageId = id,
                            PluginContext = pluginContext,
                            ApplicationContext = applicationContext,
                            StatusCode = statusCode,
                            StatusTitle = title,
                            StatusIcon = stausIcon,
                            StatusDescription = description
                        },
                        StatusPageClass = resource,
                        StatusResponse = statusResponse,
                        PluginContext = pluginContext
                    }))
                    {
                        if (defaultItem)
                        {
                            // replace existing default if marked as defaultItem
                            _defaults.AddOrUpdate(statusCode,
                                (_) => new StatusPageItem()
                                {
                                    StatusPageContext = new StatusPageContext()
                                    {
                                        StatusPageId = id,
                                        PluginContext = pluginContext,
                                        ApplicationContext = applicationContext,
                                        StatusCode = statusCode,
                                        StatusTitle = title,
                                        StatusIcon = stausIcon
                                    },
                                    StatusPageClass = resource,
                                    StatusResponse = statusResponse,
                                    PluginContext = pluginContext,
                                },
                                (_, __) => new StatusPageItem()
                                {
                                    StatusPageContext = new StatusPageContext()
                                    {
                                        StatusPageId = id,
                                        PluginContext = pluginContext,
                                        ApplicationContext = applicationContext,
                                        StatusCode = statusCode,
                                        StatusTitle = title,
                                        StatusIcon = stausIcon
                                    },
                                    StatusPageClass = resource,
                                    StatusResponse = statusResponse,
                                    PluginContext = pluginContext,
                                });
                        }
                    }
                }
            }

            Log();
        }

        /// <summary>
        /// Determines the status page for a given application context and status type.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="statusPageClass">The status page class.</param>
        /// <returns>The context of the status page or null.</returns>
        public IStatusPageContext GetStatusPage(IApplicationContext applicationContext, Type statusPageClass)
        {
            lock (_guard)
            {
                var item = _dictionary.GetStatusPageItem(applicationContext, statusPageClass);
                return item?.StatusPageContext;
            }
        }

        /// <summary>
        /// Creates a status response.
        /// </summary>
        /// <param name="message">The status message.</param>
        /// <param name="status">The status code.</param>
        /// <param name="applicationContext">The application context where the status pages are located or null for an undefined page (may be from another application) that matches the status code.</param>
        /// <param name="request">The request.</param>
        /// <returns>The response or null.</returns>
        public Response CreateStatusResponse(string message, int status, IApplicationContext applicationContext, IRequest request)
        {
            StatusPageItem statusPageItem = null;

            // access dictionary safely
            lock (_guard)
            {
                statusPageItem = _dictionary.GetStatusPageItem(applicationContext, status);
            }

            if (statusPageItem is null && _defaults.TryGetValue(status, out StatusPageItem value))
            {
                statusPageItem = value;
            }

            if (statusPageItem is null)
            {
                return status switch
                {
                    400 => new ResponseBadRequest(!string.IsNullOrWhiteSpace(message) ? new StatusMessage(message) : null),
                    401 => new ResponseUnauthorized(!string.IsNullOrWhiteSpace(message) ? new StatusMessage(message) : null),
                    403 => new ResponseForbidden(!string.IsNullOrWhiteSpace(message) ? new StatusMessage(message) : null),
                    404 => new ResponseNotFound(!string.IsNullOrWhiteSpace(message) ? new StatusMessage(message) : null),
                    500 => new ResponseInternalServerError(!string.IsNullOrWhiteSpace(message) ? new StatusMessage(message) : null),
                    _ => new ResponseInternalServerError(!string.IsNullOrWhiteSpace(message) ? new StatusMessage(message) : null),
                };
            }

            var pageInstance = ComponentActivator.CreateInstance<IComponent, IStatusPageContext>
            (
                statusPageItem.StatusPageClass,
                statusPageItem.StatusPageContext,
                _httpServerContext,
                _componentHub,
                new StatusMessage(message)
            );
            var pageType = pageInstance.GetType();
            var pageContext = new PageContext()
            {
                ApplicationContext = applicationContext,
                Scopes = new[] { typeof(IScopeStatusPage) }
            };
            var renderContext = new RenderContext(pageInstance as IEndpoint, pageContext, request);
            var visualTreeContext = new VisualTreeContext(renderContext);

            var visualTreeType = pageType.GetInterface(typeof(IStatusPage<>).Name).GetGenericArguments()[0];

            // obtain or create a cached open-instance delegate safely
            if (!_delegateCache.TryGetValue(pageType, out var del))
            {
                // create an open-instance delegate: (instance, renderContext, visualTree) => instance.Process(renderContext, visualTree)
                var instanceParam = Expression.Parameter(pageType, "instance");
                var renderContextParam = Expression.Parameter(typeof(IRenderContext), "renderContext");
                var visualTreeParam = Expression.Parameter(visualTreeType, "visualTree");

                var processMethod = pageType.GetMethod("Process", new[] { typeof(IRenderContext), visualTreeType });

                if (processMethod is null)
                {
                    throw new InvalidOperationException($"Process method not found on type {pageType.FullName}");
                }

                // call instance.Process(renderContext, visualTree)
                var callProcess = Expression.Call(instanceParam, processMethod, renderContextParam, visualTreeParam);

                // create a lambda with signature (instance, renderContext, visualTree) and compile it
                var lambda = Expression.Lambda(callProcess, instanceParam, renderContextParam, visualTreeParam).Compile();

                // try to add to concurrent dictionary atomically; if another thread added concurrently, use the existing one
                del = _delegateCache.GetOrAdd(pageType, lambda);
            }

            // create visual tree instance
            var visualTreeInstance = default(IVisualTree);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var constructors = visualTreeType?.GetConstructors(flags);

            if (constructors is not null)
            {
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

                    if (constructor.Invoke(parameterValues) is IVisualTree visualTree)
                    {
                        visualTreeInstance = visualTree;
                        break;
                    }
                }
            }
            else
            {
                visualTreeInstance = Activator.CreateInstance(visualTreeType) as IVisualTree;
            }

            if (visualTreeInstance is null)
            {
                throw new InvalidOperationException($"Could not create visual tree instance of type {visualTreeType.FullName} for page {pageType.FullName}.");
            }

            // execute the cached open-instance delegate; pass the current pageInstance
            del.DynamicInvoke(pageInstance, renderContext, visualTreeInstance);

            var response = ComponentActivator.CreateInstance<Response>(statusPageItem.StatusResponse, _httpServerContext, _componentHub, new StatusMessage(message));
            var content = visualTreeInstance.Render(new VisualTreeContext(renderContext))?.ToString();

            response.Content = content;
            response.Header.ContentLength = content?.Length ?? 0;

            return response;
        }

        /// <summary>
        /// Removes all status pages associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the status pages to remove.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            lock (_guard)
            {
                if (!_dictionary.ContainsKey(pluginContext))
                {
                    return;
                }

                _dictionary.Remove(pluginContext);
            }
        }

        /// <summary>
        /// Removes all status pages associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the status pages to remove.</param>
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
                            OnRemoveStatusPage(resourceItem.StatusPageContext);
                            resourceItem.Dispose();
                        }
                    }

                    pluginDict.Remove(applicationContext);
                }
            }
        }

        /// <summary>
        /// Raises the AddStatusPage event.
        /// </summary>
        /// <param name="statusPage">The status page.</param>
        private void OnAddStatusPage(IStatusPageContext statusPage)
        {
            AddStatusPage?.Invoke(this, statusPage);
        }

        /// <summary>
        /// Raises the RemoveComponent event.
        /// </summary>
        /// <param name="statusPage">The status page.</param>
        private void OnRemoveStatusPage(IStatusPageContext statusPage)
        {
            RemoveStatusPage?.Invoke(this, statusPage);
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
        /// Information about the component is collected and prepared for output in the log.
        /// </summary>
        private void Log()
        {
            // use snapshot StatusPages property (which is already locked) to avoid concurrent enumeration exceptions
            if (!StatusPages.Any())
            {
                return;
            }

            using var frame = new LogFrameSimple(_httpServerContext?.Log);
            var list = new List<string>
            {
                I18N.Translate("webexpress.webcore:statuspagemanager.titel")
            };

            foreach (var statusPage in StatusPages)
            {
                list.Add
                (
                    I18N.Translate("webexpress.webcore:statuspagemanager.addstatuspage", statusPage.StatusCode, statusPage.ApplicationContext?.ApplicationId)
                );
            }

            _httpServerContext?.Log?.Info(string.Join(Environment.NewLine, list));
        }

        /// <summary>
        /// Performs application-specific tasks related to sharing, returning, or resetting unmanaged resources.
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