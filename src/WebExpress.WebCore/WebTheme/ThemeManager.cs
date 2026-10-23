using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebTheme.Model;

namespace WebExpress.WebCore.WebTheme
{
    /// <summary>
    /// Manages themes for the web application.
    /// </summary>
    public class ThemeManager : IThemeManager, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly ThemeItemDictionary _itemDictionary = new();

        /// <summary>
        /// Event triggered when a theme is added.
        /// </summary>
        public event EventHandler<IThemeContext> AddTheme;

        /// <summary>
        /// Event triggered when a theme is removed.
        /// </summary>
        public event EventHandler<IThemeContext> RemoveTheme;

        /// <summary>
        /// Gets the collection of themes.
        /// </summary>
        public IEnumerable<IThemeContext> Themes => _itemDictionary.All.Select(x => x.ThemeContext);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ThemeManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:thememanager.initialization")
            );
        }

        /// <summary>
        /// Returns the theme contexts.
        /// </summary>
        /// <typeparam name="TTheme">The type of theme.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of theme contexts.</returns>
        public IEnumerable<IThemeContext> GetThemes<TTheme>(IApplicationContext applicationContext)
            where TTheme : ITheme
        {
            return GetThemes(applicationContext, typeof(TTheme));
        }

        /// <summary>
        /// Returns the theme contexts.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="themeType">The type of theme.</param>
        /// <returns>An IEnumerable of theme contexts.</returns>
        public IEnumerable<IThemeContext> GetThemes(IApplicationContext applicationContext, Type themeType)
        {
            return _itemDictionary.GetThemeItems(applicationContext)
                .Where(x => x.ThemeClass == themeType)
                .Select(x => x.ThemeContext);
        }

        /// <summary>
        /// Returns the theme associated with the specified theme context.
        /// </summary>
        /// <param name="themeContext">The context of the theme to retrieve.</param>
        /// <returns>The theme associated with the specified context.</returns>
        public ITheme GetTheme(IThemeContext themeContext)
        {
            return _itemDictionary.GetThemeItems(themeContext.ApplicationContext)
                .Where(x => x.ThemeContext == themeContext)
                .Select(x => x.Instance)
                .FirstOrDefault();
        }

        /// <summary>
        /// Discovers and binds resources to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose resources are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (_itemDictionary.ContainsPlugin(pluginContext))
            {
                return;
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
                if (_itemDictionary.ContainsApplication(pluginContext, applicationContext))
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
            var assembly = pluginContext?.Assembly;
            var assemblName = assembly.GetName().Name;
            var themeTypes = assembly.GetTypes().Where
            (
                x => x.IsClass == true &&
                x.IsSealed &&
                x.IsPublic &&
                (
                    x.GetInterface(typeof(ITheme).Name) is not null
                )
            );

            foreach (var themeType in themeTypes)
            {
                var id = themeType.FullName?.ToLower();
                var image = default(string);
                var name = default(string);
                var description = default(string);
                var mode = ThemeMode.Light;
                var style = default(string);

                foreach (var customAttribute in themeType.CustomAttributes
                    .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(IThemeAttribute))))
                {
                    if (customAttribute.AttributeType == typeof(ImageAttribute))
                    {
                        image ??= customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(NameAttribute))
                    {
                        name ??= customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DescriptionAttribute))
                    {
                        description ??= customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(ThemeModeAttribute))
                    {
                        try
                        {
                            mode = Enum.Parse<ThemeMode>(customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString());
                        }
                        catch
                        {
                            mode = ThemeMode.Light;
                        }
                    }
                    else if (customAttribute.AttributeType == typeof(ThemeStyleAttribute))
                    {
                        style ??= customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                }

                // assign the theme to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var themeContext = new ThemeContext()
                    {
                        ThemeId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        Name = name,
                        Description = description,
                        Image = image is not null ? RouteEndpoint.Combine(applicationContext.Route, image) : null,
                        ThemeMode = mode,
                        ThemeStyle = style is not null ? RouteEndpoint.Combine(applicationContext.Route, style) : null,
                    };

                    var themeItem = new ThemeItem()
                    {
                        ThemeClass = themeType,
                        ThemeContext = themeContext,
                        Instance = ComponentActivator.CreateInstance<ITheme, IThemeContext>(themeType, themeContext, _httpServerContext, _componentHub, themeType),
                    };

                    if (_itemDictionary.AddThemeItem(pluginContext, applicationContext, themeItem))
                    {
                        OnAddTheme(themeContext);
                        _httpServerContext?.Log?.Debug(
                            I18N.Translate(
                                "webexpress.webcore:thememanager.addtheme",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                }
            }

            Log();
        }

        /// <summary>
        /// Removes all resources associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the resources to remove.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            foreach (var themeContext in _itemDictionary.Remove(pluginContext))
            {
                OnRemoveTheme(themeContext);
            }
        }

        /// <summary>
        /// Removes all assets associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the resources to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            foreach (var assetContext in _itemDictionary.Remove(applicationContext))
            {
                OnRemoveTheme(assetContext);
            }
        }

        /// <summary>
        /// Raises the AddTheme event.
        /// </summary>
        /// <param name="themeContext">The theme context.</param>
        private void OnAddTheme(IThemeContext themeContext)
        {
            AddTheme?.Invoke(this, themeContext);
        }

        /// <summary>
        /// Raises the RemoveTheme event.
        /// </summary>
        /// <param name="themeContext">The theme context.</param>
        private void OnRemoveTheme(IThemeContext themeContext)
        {
            RemoveTheme?.Invoke(this, themeContext);
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
        /// Information about the component is collected and prepared for output in the log.
        /// </summary>
        private void Log()
        {
            if (!Themes.Any())
            {
                return;
            }

            using var frame = new LogFrameSimple(_httpServerContext?.Log);
            var list = new List<string>
            {
                I18N.Translate("webexpress.webcore:thememanager.titel")
            };

            foreach (var eventHandlerContext in Themes)
            {
                list.Add
                (
                    I18N.Translate("webexpress.webcore:thememanager.theme", eventHandlerContext.ThemeId, eventHandlerContext.ApplicationContext?.ApplicationId)
                );
            }

            _httpServerContext?.Log?.Info(string.Join(Environment.NewLine, list));
        }

        /// <summary>
        /// Releases all resources used by the ThemeManager.
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
