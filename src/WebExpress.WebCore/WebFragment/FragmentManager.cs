using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebFragment.Model;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebScope;
using WebExpress.WebCore.WebSection;

namespace WebExpress.WebCore.WebFragment
{
    /// <summary>
    /// The fragment manager. Fragments are independent parts of a page.
    /// </summary>
    public sealed class FragmentManager : IFragmentManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly FragmentDictionary _dictionary = new();

        /// <summary>
        /// An event that fires when an fragment is added.
        /// </summary>
        public event EventHandler<IFragmentContext> AddFragment;

        /// <summary>
        /// An event that fires when an fragment is removed.
        /// </summary>
        public event EventHandler<IFragmentContext> RemoveFragment;

        /// <summary>
        /// Gets the collection of fragment contexts.
        /// </summary>
        public IEnumerable<IFragmentContext> Fragments => _dictionary.All;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private FragmentManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:fragmentmanager.initialization")
            );
        }

        /// <summary>
        /// Discovers and binds fragments to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose fragments are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (_dictionary.Contains(pluginContext))
            {
                return;
            }

            Register(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
        }

        /// <summary>
        /// Discovers and binds fragments to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose fragments are to be associated.</param>
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
            var assembly = pluginContext.Assembly;

            foreach (var fragmentType in assembly.GetTypes()
                .Where(x => x.IsClass == true && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(IFragment<,>).Name) is not null))
            {
                var id = fragmentType.FullName?.ToLower();
                var scopes = new List<Type>();
                var sections = new List<Type>();
                var conditions = new List<ICondition>();
                var policies = new List<IIdentityPolicy>();
                var cache = false;
                var order = 0;

                // determining attributes
                foreach (var customAttribute in fragmentType.CustomAttributes.Where
                (
                    x => x.AttributeType.GetInterfaces()
                            .Contains(typeof(IEndpointAttribute))
                ))
                {
                    if (customAttribute.AttributeType.Name == typeof(ScopeAttribute<>).Name && customAttribute.AttributeType.Namespace == typeof(ScopeAttribute<>).Namespace)
                    {
                        scopes.Add(customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault());
                    }
                    else if (customAttribute.AttributeType.Name == typeof(ConditionAttribute<>).Name && customAttribute.AttributeType.Namespace == typeof(ConditionAttribute<>).Namespace)
                    {
                        var condition = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();
                        conditions.Add(Activator.CreateInstance(condition) as ICondition);
                    }
                    else if (customAttribute.AttributeType.Name == typeof(PolicyAttribute<>).Name && customAttribute.AttributeType.Namespace == typeof(PolicyAttribute<>).Namespace)
                    {
                        var policy = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();
                        policies.Add(Activator.CreateInstance(policy) as IIdentityPolicy);
                    }
                    else if (customAttribute.AttributeType == typeof(CacheAttribute))
                    {
                        cache = true;
                    }
                }

                foreach (var customAttribute in fragmentType.CustomAttributes.Where
                (
                    x => x.AttributeType.GetInterfaces().Contains(typeof(IFragmentAttribute))
                ))
                {
                    if (customAttribute.AttributeType.Name == typeof(SectionAttribute<>).Name && customAttribute.AttributeType.Namespace == typeof(SectionAttribute<>).Namespace)
                    {
                        sections.Add(customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault());
                    }
                    else if (customAttribute.AttributeType == typeof(OrderAttribute))
                    {
                        try
                        {
                            order = Convert.ToInt32(customAttribute.ConstructorArguments.FirstOrDefault().Value);
                        }
                        catch
                        {
                        }
                    }
                }

                // check section
                if (sections.Count == 0)
                {
                    _httpServerContext?.Log?.Warning(I18N.Translate
                    (
                        "webexpress.webcore:fragmentmanager.error.section"
                    ));

                    continue;
                }

                // check scope
                if (scopes.Count == 0)
                {
                    scopes.Add(typeof(IScope));
                }

                // assign the fragment to existing applications
                foreach (var applicationContext in _componentHub?.ApplicationManager.GetApplications(pluginContext))
                {
                    // assign section
                    foreach (var section in sections)
                    {
                        // assign scope
                        foreach (var scope in scopes)
                        {
                            var fragmentContext = new FragmentContext()
                            {
                                PluginContext = pluginContext,
                                ApplicationContext = applicationContext,
                                FragmentId = new ComponentId(id),
                                Cache = cache,
                                Section = section,
                                Scope = scope,
                                Conditions = conditions,
                                Policies = policies
                            };

                            var fragmentItem = new FragmentItem(_componentHub, _httpServerContext)
                            {
                                PluginContext = pluginContext,
                                ApplicationContext = applicationContext,
                                FragmentContext = fragmentContext,
                                FragmentClass = fragmentType,
                                Order = order,
                                Cache = cache,
                                Conditions = conditions,
                                Section = section,
                                Scope = scope
                            };

                            if (_dictionary.AddFragmentItem(pluginContext, applicationContext, fragmentItem))
                            {
                                OnAddFragment(fragmentContext);

                                _httpServerContext?.Log?.Debug
                                (
                                    I18N.Translate
                                    (
                                        "webexpress.webcore:fragmentmanager.register",
                                        id,
                                        section,
                                        applicationContext.ApplicationId
                                    )
                                );
                            }
                        }
                    }
                }
            }

            Log();
        }

        /// <summary>
        /// Removes all components associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the components to remove.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            var fragments = _dictionary.RemoveFragments(pluginContext);

            foreach (var fragment in fragments)
            {
                OnRemoveFragment(fragment);
            }
        }

        /// <summary>
        /// Removes all fragments associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the fragments to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

            var fragments = _dictionary.RemoveFragments(applicationContext);

            foreach (var fragment in fragments)
            {
                OnRemoveFragment(fragment);
            }
        }

        /// <summary>
        /// Raises the AddFragment event.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        private void OnAddFragment(IFragmentContext fragmentContext)
        {
            AddFragment?.Invoke(this, fragmentContext);
        }

        /// <summary>
        /// Raises the RemoveFragment event.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        private void OnRemoveFragment(IFragmentContext fragmentContext)
        {
            RemoveFragment?.Invoke(this, fragmentContext);
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
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <typeparam name="T">The fragment type.</typeparam>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments<T>() where T : IFragmentBase
        {
            return GetFragments(typeof(T));
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <param name="fragmentType">The fragment type.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(Type fragmentType)
        {
            return _dictionary.GetFragments(fragmentType);
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments<TFragment>(IApplicationContext applicationContext)
            where TFragment : IFragmentBase
        {
            return GetFragments(applicationContext, typeof(TFragment));
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="fragmentType">The fragment type.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type fragmentType)
        {
            return _dictionary.GetFragments(applicationContext, fragmentType);
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <typeparam name="TScope">The scope where the fragment is embedded.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments<TSection, TScope>(IApplicationContext applicationContext)
            where TSection : ISection
            where TScope : IScope
        {
            return GetFragments(applicationContext, typeof(TSection), typeof(TScope));
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <param name="scope">The scope where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type section, Type scope)
        {
            return _dictionary.GetFragments(applicationContext, section, scope);
        }

        /// <summary>
        /// Returns all fragments that belong to a given application.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="scopes">The scopes where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragments.</returns>
        public IEnumerable<TFragment> GetFragments<TFragment, TSection>(IApplicationContext applicationContext, IEnumerable<Type> scopes)
            where TFragment : IFragmentBase
            where TSection : ISection
        {
            var effectiveScopes = (scopes?.Any() == true) ? scopes : [typeof(IScope)];

            foreach (var item in _dictionary.GetFragmentItems(applicationContext, typeof(TFragment), typeof(TSection), effectiveScopes))
            {
                yield return item.CreateInstance<TFragment>();
            }
        }

        /// <summary>
        /// Returns all fragments that belong to a given page.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <param name="pageContext">The page context.</param>
        /// <returns>An enumeration of the filtered fragments.</returns>
        public IEnumerable<TFragment> GetFragments<TFragment, TSection>(IPageContext pageContext)
            where TFragment : IFragmentBase
            where TSection : ISection
        {
            var applicationContext = pageContext?.ApplicationContext;
            var scopes = pageContext?.Scopes ?? [typeof(IScope)];

            var effectiveScopes = (scopes?.Any() == true) ? scopes : [typeof(IScope)];

            foreach (var item in _dictionary.GetFragmentItems(applicationContext, typeof(TFragment), typeof(TSection), effectiveScopes))
            {
                yield return item.CreateInstance<TFragment>(pageContext);
            }
        }

        /// <summary>
        /// Returns the fragments of a page that may appear for the given request. A control that
        /// only reads properties of a fragment, instead of rendering it, never reaches the check in
        /// the fragment's own render method, so the conditions and policies are evaluated here.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <param name="pageContext">The page context.</param>
        /// <param name="request">The request whose state and identity decide which fragments are shown.</param>
        /// <returns>An enumeration of the fragments whose conditions and policies the request fulfills.</returns>
        public IEnumerable<TFragment> GetFragments<TFragment, TSection>(IPageContext pageContext, IRequest request)
            where TFragment : IFragmentBase
            where TSection : ISection
        {
            var applicationContext = pageContext?.ApplicationContext;
            var scopes = pageContext?.Scopes ?? [typeof(IScope)];

            var effectiveScopes = (scopes?.Any() == true) ? scopes : [typeof(IScope)];

            foreach (var item in _dictionary.GetFragmentItems(applicationContext, typeof(TFragment), typeof(TSection), effectiveScopes))
            {
                // checked before the instance is created, so a hidden fragment is never constructed
                if (item.FragmentContext.Check(request))
                {
                    yield return item.CreateInstance<TFragment>(pageContext);
                }
            }
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <param name="scopes">The scopes where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type section, IEnumerable<Type> scopes)
        {
            var effectiveScopes = (scopes?.Any() == true) ? scopes : [typeof(IScope)];

            foreach (var scope in effectiveScopes)
            {
                foreach (var item in GetFragments(applicationContext, section, effectiveScopes))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Converts the fragments to HTML for a given section within the specified render context.
        /// </summary>
        /// <typeparam name="TRenderContext">The type of the render context.</typeparam>
        /// <typeparam name="TVisualTree">The type of the visual tree.</typeparam>
        /// <param name="renderContext">The context in which rendering occurs.</param>
        /// <param name="visualTree">The visual tree used for rendering.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <returns>An enumeration of HTML nodes representing the rendered fragments.</returns>
        public IEnumerable<IHtmlNode> Render<TRenderContext, TVisualTree>(TRenderContext renderContext, TVisualTree visualTree, Type section)
            where TRenderContext : IRenderContext
            where TVisualTree : IVisualTree
        {
            var applicationContext = renderContext?.PageContext?.ApplicationContext;
            var scopes = renderContext?.PageContext?.Scopes ?? [typeof(IScope)];
            var items = _dictionary.GetFragmentItems(applicationContext, section, scopes);

            return items.Select(x => x.Render(renderContext, visualTree));
        }

        /// <summary>
        /// Information about the component is collected and prepared for output in the log.
        /// </summary>
        private void Log()
        {
            if (!Fragments.Any())
            {
                return;
            }

            using var frame = new LogFrameSimple(_httpServerContext?.Log);
            var list = new List<string>
            {
                I18N.Translate("webexpress.webcore:fragmentmanager.titel")
            };

            foreach (var fragment in Fragments.Distinct())
            {
                list.Add
                (
                    string.Empty.PadRight(2) +
                    I18N.Translate("webexpress.webcore:fragmentmanager.fragment", fragment.FragmentId.ToString(), fragment.ApplicationContext?.ApplicationId)
                );
            }

            _httpServerContext?.Log?.Info(string.Join(Environment.NewLine, list));
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
