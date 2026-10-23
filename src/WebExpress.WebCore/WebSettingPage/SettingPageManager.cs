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
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebScope;
using WebExpress.WebCore.WebSettingPage.Model;

namespace WebExpress.WebCore.WebSettingPage
{
    /// <summary>
    /// Management of settings pages.
    /// </summary>
    public sealed class SettingPageManager : ISettingPageManager, IDisposable
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly SettingCategoryDictionary _categoryDictionary = new();
        private readonly SettingGroupDictionary _groupDictionary = new();
        private readonly SettingPageDictionary _pageDictionary = new();

        // use a concurrent dictionary for delegate cache to avoid concurrent write corruption
        private static readonly ConcurrentDictionary<Type, Delegate> _delegateCache = new ConcurrentDictionary<Type, Delegate>();

        /// <summary>
        /// An event that fires when an setting page is added.
        /// </summary>
        public event EventHandler<ISettingPageContext> AddSettingPage;

        /// <summary>
        /// An event that fires when an setting page is removed.
        /// </summary>
        public event EventHandler<ISettingPageContext> RemoveSettingPage;

        /// <summary>
        /// An event that fires when an setting category is added.
        /// </summary>
        public event EventHandler<ISettingCategoryContext> AddSettingCategory;

        /// <summary>
        /// An event that fires when an setting category is removed.
        /// </summary>
        public event EventHandler<ISettingCategoryContext> RemoveSettingCategory;

        /// <summary>
        /// An event that fires when an setting group is added.
        /// </summary>
        public event EventHandler<ISettingGroupContext> AddSettingGroup;

        /// <summary>
        /// An event that fires when an setting group is removed.
        /// </summary>
        public event EventHandler<ISettingGroupContext> RemoveSettingGroup;

        /// <summary>
        /// Gets the collection of setting categories.
        /// </summary>
        public IEnumerable<ISettingCategoryContext> SettingCategories => _categoryDictionary.All;

        /// <summary>
        /// Gets the collection of setting groups.
        /// </summary>
        public IEnumerable<ISettingGroupContext> SettingGroups => _groupDictionary.All;

        /// <summary>
        /// Gets the collection of setting pages.
        /// </summary>
        public IEnumerable<ISettingPageContext> SettingPages => _pageDictionary.All;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private SettingPageManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
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
                    ? GetSettingPages(type, applicationContext)
                    : GetSettingPages(type),
                EndpointsResolver = () => SettingPages,
                HandleRequest = (request, endpontContext) =>
                {
                    // create or obtain page instance for this request
                    var pageInstance = CreateSettingPageInstance(endpontContext as ISettingPageContext);
                    var pageType = pageInstance.GetType();
                    var pageContext = endpontContext as IPageContext;
                    var renderContext = new RenderContext(pageInstance, pageContext, request);
                    var visualTreeContext = new VisualTreeContext(renderContext);

                    // determine visual tree type implemented by the setting page
                    var pageInterface = pageType.GetInterface(typeof(ISettingPage<>).Name);
                    if (pageInterface is null)
                    {
                        throw new InvalidOperationException($"Page type {pageType.FullName} does not implement ISettingPage<>.");
                    }

                    var visualTreeType = pageInterface.GetGenericArguments()[0];

                    // obtain or create a cached open-instance delegate safely
                    if (!_delegateCache.TryGetValue(pageType, out var del))
                    {
                        // create an open-instance delegate: (instance, renderContext, visualTree) => instance.Process(renderContext, visualTree)
                        var instanceParam = Expression.Parameter(pageType, "instance");
                        var renderContextParam = Expression.Parameter(typeof(IRenderContext), "renderContext");
                        var visualTreeParam = Expression.Parameter(visualTreeType, "visualTree");

                        // find Process method matching signature Process(IRenderContext, TVisualTree)
                        var processMethod = pageType.GetMethod("Process", new[] { typeof(IRenderContext), visualTreeType });
                        if (processMethod is null)
                        {
                            throw new InvalidOperationException($"Process method not found on type {pageType.FullName}");
                        }

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
                        foreach (var constructor in constructors.OrderByDescending(x => x.GetParameters().Length))
                        {
                            // injection
                            var parameters = constructor.GetParameters();
                            var hubProperties = _componentHub?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                            var contextIdProperty = pageContext?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(x => x.PropertyType == typeof(IComponentId))
                                .FirstOrDefault();

                            var parameterValues = parameters.Select(parameter =>
                                parameter.ParameterType == typeof(IComponentHub) ? _componentHub :
                                parameter.ParameterType == typeof(IHttpServerContext) ? _httpServerContext :
                                parameter.ParameterType == typeof(IPageContext) ? pageContext :
                                parameter.ParameterType == typeof(IComponentId) ? contextIdProperty?.GetValue(pageContext) :
                                hubProperties.Where(x => x.PropertyType == parameter.ParameterType)
                                    .FirstOrDefault()?
                                    .GetValue(_componentHub) ?? null
                            ).ToArray();

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

                    return new ResponseOK()
                    {
                        Content = visualTreeInstance.Render(visualTreeContext)
                    };
                }
            };

            AddSettingPage += (sender, e) => endpointtRegistration.AddEndpoint?.Invoke(sender, e);
            RemoveSettingPage += (sender, e) => endpointtRegistration.RemoveEndpoint?.Invoke(sender, e);

            _componentHub?.EndpointManager.Register<SettingPageContext>(endpointtRegistration);

            _httpServerContext?.Log?.Debug(I18N.Translate("webexpress.webcore:settingpagemanager.initialization"));
        }

        /// <summary>
        /// Creates a new setting page and returns it. If a page already exists (through caching), the existing instance is returned.
        /// </summary>
        /// <param name="settingPageContext">The context used for setting page creation.</param>
        /// <returns>The created or cached page.</returns>
        private IEndpoint CreateSettingPageInstance(ISettingPageContext settingPageContext)
        {
            return _pageDictionary.CreateSettingPageInstance(settingPageContext, _componentHub, _httpServerContext);
        }

        /// <summary>
        /// Discovers and binds setting pages to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose setting pages are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (_pageDictionary.Contains(pluginContext))
            {
                return;
            }

            RegisterCategory(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
            RegisterGroup(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
            RegisterPage(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
        }

        /// <summary>
        /// Discovers and binds setting pages to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose pages are to be associated.</param>
        private void Register(IApplicationContext applicationContext)
        {
            if (_pageDictionary.Contains(applicationContext))
            {
                return;
            }

            foreach (var pluginContext in _componentHub?.PluginManager?.GetPlugins(applicationContext))
            {
                RegisterCategory(pluginContext, new[] { applicationContext });
                RegisterGroup(pluginContext, new[] { applicationContext });
                RegisterPage(pluginContext, new[] { applicationContext });
            }
        }

        /// <summary>
        /// Registers categories for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void RegisterCategory(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var assembly = pluginContext.Assembly;

            foreach (var settingCategoryType in assembly.GetTypes()
                .Where(x => x.IsClass == true && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(ISettingCategory).Name) is not null))
            {
                var id = settingCategoryType.FullName?.ToLower();
                var icon = default(IIcon);
                var name = default(string);
                var description = default(string);
                var section = SettingSection.Primary;

                // determining attributes
                foreach (var customAttribute in settingCategoryType.CustomAttributes
                    .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(ISettingCategoryAttribute))))
                {
                    if (customAttribute.AttributeType.IsGenericType && customAttribute.AttributeType.GetGenericTypeDefinition() == typeof(WebIconAttribute<>))
                    {
                        var type = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();
                        icon ??= Activator.CreateInstance(type) as IIcon;
                    }
                    else if (customAttribute.AttributeType == typeof(NameAttribute))
                    {
                        name ??= customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DescriptionAttribute))
                    {
                        description ??= customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(SettingSectionAttribute))
                    {
                        section = Enum.Parse<SettingSection>(customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString());
                    }
                }

                // assign the category to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var settingCategoryContext = new SettingCategoryContext()
                    {
                        CategoryId = new ComponentId(id),
                        ApplicationContext = applicationContext,
                        PluginContext = pluginContext,
                        Icon = icon,
                        Name = name,
                        Description = description,
                        Section = section
                    };

                    // create meta information of the setting category
                    var settingCategoryItem = new SettingCategoryItem()
                    {
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        SettingCategoryContext = settingCategoryContext,
                        SettingCategoryClass = settingCategoryType,
                        Name = name,
                        Description = description,
                        Section = section
                    };

                    // insert the settings category into the dictionary
                    if (_categoryDictionary.AddSettingCategoryItem(settingCategoryItem))
                    {
                        OnAddSettingCategory(settingCategoryContext);

                        _httpServerContext?.Log?.Debug(I18N.Translate("webexpress.webcore:settingpagemanager.register.category", id, applicationContext.ApplicationId));
                    }
                }
            }
        }

        /// <summary>
        /// Registers groups for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void RegisterGroup(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var assembly = pluginContext.Assembly;

            foreach (var settingGroupType in assembly.GetTypes()
                .Where(x => x.IsClass == true && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(ISettingGroup).Name) is not null))
            {
                var id = settingGroupType.FullName?.ToLower();
                var icon = default(IIcon);
                var name = default(string);
                var description = default(string);
                var category = default(Type);
                var section = SettingSection.Primary;

                // determining attributes
                foreach (var customAttribute in settingGroupType.CustomAttributes
                    .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(ISettingGroupAttribute))))
                {
                    if (customAttribute.AttributeType.IsGenericType && customAttribute.AttributeType.GetGenericTypeDefinition() == typeof(WebIconAttribute<>))
                    {
                        var type = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();
                        icon ??= Activator.CreateInstance(type) as IIcon;
                    }
                    else if (customAttribute.AttributeType == typeof(NameAttribute))
                    {
                        name = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DescriptionAttribute))
                    {
                        description = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType.IsGenericType && customAttribute.AttributeType.GetGenericTypeDefinition() == typeof(SettingCategoryAttribute<>))
                    {
                        category = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();
                    }
                    else if (customAttribute.AttributeType == typeof(SettingSectionAttribute))
                    {
                        section = Enum.Parse<SettingSection>(customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString());
                    }
                }

                if (category == default)
                {
                    _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:settingpagemanager.register.nocategory", id));
                }

                // assign the group to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var settingGroupContext = new SettingGroupContext()
                    {
                        GroupId = new ComponentId(id),
                        ApplicationContext = applicationContext,
                        PluginContext = pluginContext,
                        Icon = icon,
                        Name = name,
                        Description = description,
                        SettingCategory = _categoryDictionary.GetSettingCategory(applicationContext, category),
                        Section = section
                    };

                    // create meta information of the setting group
                    var settingGroupItem = new SettingGroupItem()
                    {
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        SettingGroupContext = settingGroupContext,
                        SettingGroupClass = settingGroupType,
                        Name = name,
                        Description = description,
                        Category = category?.GetType(),
                        Section = section
                    };

                    // insert the settings category into the dictionary
                    if (_groupDictionary.AddSettingGroupItem(settingGroupItem))
                    {
                        OnAddSettingGroup(settingGroupContext);

                        _httpServerContext?.Log?.Debug(I18N.Translate("webexpress.webcore:settingpagemanager.register.group", id, applicationContext.ApplicationId));
                    }
                }
            }
        }

        /// <summary>
        /// Registers pages for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void RegisterPage(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var assembly = pluginContext.Assembly;

            foreach (var settingPageType in assembly.GetTypes()
                .Where(x => x.IsClass == true && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(ISettingPage<>).Name) is not null))
            {
                var id = settingPageType.FullName?.ToLower();
                var title = settingPageType.Name;
                var segment = default(ISegmentAttribute);
                var conditions = new List<ICondition>();
                var contextPath = string.Empty;
                var scopes = new List<Type>();
                var group = default(Type);
                var domains = new List<Type>();
                var section = SettingSection.Primary;
                var includeSubPaths = false;
                var hide = false;
                var icon = default(Type);
                var cache = false;
                var policies = new List<IIdentityPolicy>();
                var attributes = settingPageType.CustomAttributes
                    .Where(x => !x.AttributeType.GetInterfaces().Contains(typeof(IEndpointAttribute)) &&
                                !x.AttributeType.GetInterfaces().Contains(typeof(IPageAttribute)));

                // determining attributes
                foreach
                (
                    var attribute in settingPageType
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

                    // setting group (generic)
                    if (attributeType.IsGenericType &&
                        attributeType.GetGenericTypeDefinition() == typeof(SettingGroupAttribute<>))
                    {
                        group = attributeType.GetGenericArguments().FirstOrDefault();
                        continue;
                    }

                    // setting section
                    if (attributeType == typeof(SettingSectionAttribute))
                    {
                        section = (attribute as SettingSectionAttribute)?.Section ?? default;
                        continue;
                    }

                    // setting hide
                    if (attributeType == typeof(SettingHideAttribute))
                    {
                        hide = true;
                        continue;
                    }

                    // web icon attribute (generic)
                    if (attributeType.IsGenericType &&
                        attributeType.GetGenericTypeDefinition() == typeof(WebIconAttribute<>))
                    {
                        icon = attributeType.GetGenericArguments().FirstOrDefault();
                        continue;
                    }

                    // cache attribute
                    if (attributeType == typeof(CacheAttribute))
                    {
                        cache = true;
                        continue;
                    }

                    // include subpaths
                    if (attributeType == typeof(IncludeSubPathsAttribute))
                    {
                        includeSubPaths = (attribute as IncludeSubPathsAttribute)?.IncludeSubPaths ?? false;
                        continue;
                    }

                    // condition attribute (generic)
                    if (attributeType.IsGenericType &&
                        attributeType.GetGenericTypeDefinition().Name == typeof(ConditionAttribute<>).Name &&
                        attributeType.Namespace == typeof(ConditionAttribute<>).Namespace)
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
                }

                if (group == default)
                {
                    _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:settingpagemanager.register.nogroup", id));
                }

                foreach
                (
                    var attribute in settingPageType
                        .GetCustomAttributes(inherit: true)
                        .Where(x => x.GetType().GetInterfaces().Contains(typeof(ISettingPageAttribute)))
                )
                {
                    var attributeType = attribute.GetType();

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

                if (settingPageType.GetInterfaces().Where(x => x == typeof(IScope)).Any())
                {
                    scopes.Add(settingPageType);
                }

                // assign the setting page to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var prefix = applicationContext.Route.Concat(applicationContext.PluginContext != pluginContext ? pluginContext.PluginName.ToLower() : "");

                    var routePath = EndpointManager.CreateEndpointRoute(settingPageType, prefix, segment);

                    var settingPageContext = new SettingPageContext()
                    {
                        EndpointId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        Route = routePath,
                        Cache = cache,
                        Conditions = conditions,
                        IncludeSubPaths = includeSubPaths,
                        Attributes = EndpointManager.GetAttributeInstances(attributes),
                        PageTitle = title,
                        PageIcon = GetIcon(icon),
                        Scopes = scopes,
                        Domains = domains,
                        SettingGroup = _groupDictionary.GetSettingGroup(applicationContext, group),
                        Section = section,
                        Policies = policies,
                        Hide = hide
                    };

                    // create meta information of the setting page
                    var settingPageItem = new SettingPageItem(_componentHub?.EndpointManager)
                    {
                        EndpointId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        SettingPageContext = settingPageContext,
                        SettingPageClass = settingPageType,
                        SettingGroup = settingPageContext.SettingGroup,
                        SettingGroupType = group?.GetType(),
                        IncludeSubPaths = includeSubPaths,
                        Attributes = attributes.Select(x => x.AttributeType)
                    };

                    // insert the settings page into the dictionary
                    if (_pageDictionary.AddSettingPageItem(settingPageItem))
                    {
                        OnAddSettingPage(settingPageItem.SettingPageContext);

                        _httpServerContext?.Log?.Debug(I18N.Translate("webexpress.webcore:settingpagemanager.register.page", id, applicationContext.ApplicationId));
                    }
                }
            }
        }

        /// <summary>
        /// Removes all elemets associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the elemets to remove.</param>
        public void Remove(IPluginContext pluginContext)
        {
            _pageDictionary.Remove(pluginContext);
        }

        /// <summary>
        /// Removes all setting pages associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the fragments to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            foreach (var settingPageContext in _pageDictionary.Remove(applicationContext))
            {
                OnRemoveSettingPage(settingPageContext);
            }
        }

        /// <summary>
        /// Returns an enumeration of setting page contextes.
        /// </summary>
        /// <param name="settingPageType">The setting page type.</param>
        /// <returns>An enumeration of setting page contextes.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(Type settingPageType)
        {
            return _pageDictionary.GetSettingPages(settingPageType);
        }

        /// <summary>
        /// Returns an enumeration of setting page contexts.
        /// </summary>
        /// <param name="settingPageType">The type of the setting page.</param>
        /// <param name="applicationContext">The application context in which the setting pages are retrieved.</param>
        /// <returns>An enumeration of setting page contexts.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(Type settingPageType, IApplicationContext applicationContext)
        {
            return _pageDictionary.GetSettingPages(settingPageType, applicationContext);
        }

        /// <summary>
        /// Returns the category contexts associated with the given application context.
        /// </summary>
        /// <param name="applicationContext">The application context for which the categories are to be retrieved.</param>
        /// <returns>An enumeration of category contexts associated with the specified application context.</returns>
        public IEnumerable<ISettingCategoryContext> GetSettingCategories(IApplicationContext applicationContext)
        {
            return _categoryDictionary.GetSettingCategories(applicationContext);
        }

        /// <summary>
        /// Returns the setting groups associated with the specified application context and category.
        /// </summary>
        /// <param name="applicationContext">The application context used to identify the relevant groups.</param>
        /// <param name="categoryContext">The category context for filtering the setting groups.</param>
        /// <returns>An enumeration of setting group contexts associated with the provided application context and category.</returns>
        public IEnumerable<ISettingGroupContext> GetSettingGroups(IApplicationContext applicationContext, ISettingCategoryContext categoryContext)
        {
            return _groupDictionary.GetSettingGroups(applicationContext, categoryContext);
        }

        /// <summary>
        /// Returns an enumeration of setting page contexts for the specified application context and category.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="categoryContext">The category for which to retrieve setting pages.</param>
        /// <returns>An enumeration of setting page contexts.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(IApplicationContext applicationContext, ISettingCategoryContext categoryContext)
        {
            return _pageDictionary.GetSettingPages(applicationContext, categoryContext);
        }

        /// <summary>
        /// Returns an enumeration of setting page contexts for the specified 
        /// application context, category, and group.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="groupContext">The group for which to retrieve setting pages.</param>
        /// <returns>An enumeration of setting page contexts.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(IApplicationContext applicationContext, ISettingGroupContext groupContext)
        {
            return _pageDictionary.GetSettingPages(applicationContext, groupContext);
        }

        /// <summary>
        /// Returns the first setting page context for the specified application context and category.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="categoryContext">The category for which to retrieve setting pages.</param>
        /// <returns>The first setting page context or null.</returns>
        public ISettingPageContext GetFirstSettingPage(IApplicationContext applicationContext, ISettingCategoryContext categoryContext)
        {
            var pages = _pageDictionary.GetSettingPages(applicationContext, categoryContext);
            var preferences = pages.Where(x => x.Section == SettingSection.Preferences);
            var primary = pages.Where(x => x.Section == SettingSection.Primary);
            var secondary = pages.Where(x => x.Section == SettingSection.Secondary);

            if (preferences.Any())
            {
                return preferences.OrderBy(x => x.PageTitle).FirstOrDefault();
            }

            if (primary.Any())
            {
                return primary.OrderBy(x => x.PageTitle).FirstOrDefault();
            }

            if (secondary.Any())
            {
                return secondary.OrderBy(x => x.PageTitle).FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// Raises the AddSettingPage event.
        /// </summary>
        /// <param name="settingPageContext">The setting page context.</param>
        private void OnAddSettingPage(ISettingPageContext settingPageContext)
        {
            AddSettingPage?.Invoke(this, settingPageContext);
        }

        /// <summary>
        /// Raises the RemoveSettingPage event.
        /// </summary>
        /// <param name="settingPageContext">The setting page context.</param>
        private void OnRemoveSettingPage(ISettingPageContext settingPageContext)
        {
            RemoveSettingPage?.Invoke(this, settingPageContext);
        }

        /// <summary>
        /// Raises the AddSettingCategory event.
        /// </summary>
        /// <param name="settingCategoryeContext">The setting category context.</param>
        private void OnAddSettingCategory(ISettingCategoryContext settingCategoryeContext)
        {
            AddSettingCategory?.Invoke(this, settingCategoryeContext);
        }

        /// <summary>
        /// Raises the RemoveSettingCategory event.
        /// </summary>
        /// <param name="settingCategoryContext">The setting category context.</param>
        private void OnRemoveSettingCategory(ISettingCategoryContext settingCategoryContext)
        {
            RemoveSettingCategory?.Invoke(this, settingCategoryContext);
        }

        /// <summary>
        /// Raises the AddSettingGroup event.
        /// </summary>
        /// <param name="settingGroupContext">The setting group context.</param>
        private void OnAddSettingGroup(ISettingGroupContext settingGroupContext)
        {
            AddSettingGroup?.Invoke(this, settingGroupContext);
        }

        /// <summary>
        /// Raises the RemoveSettingGroup event.
        /// </summary>
        /// <param name="settingGroupContext">The setting group context.</param>
        private void OnRemoveSettingGroup(ISettingGroupContext settingGroupContext)
        {
            RemoveSettingGroup?.Invoke(this, settingGroupContext);
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