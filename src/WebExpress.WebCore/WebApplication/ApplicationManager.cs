using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication.Model;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// Management of WebExpress applications.
    /// </summary>
    public sealed class ApplicationManager : IApplicationManager, IExecutableElements, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly ApplicationDictionary _dictionary = new();
        private readonly Lock _failuresSync = new();
        private readonly List<ApplicationFailure> _failures = [];

        /// <summary>
        /// An event that fires when an application is added.
        /// </summary>
        public event EventHandler<IApplicationContext> AddApplication;

        /// <summary>
        /// An event that fires when an application is removed.
        /// </summary>
        public event EventHandler<IApplicationContext> RemoveApplication;

        /// <summary>
        /// An event that fires when the name or the icon of a registered application changed.
        /// </summary>
        public event EventHandler<IApplicationContext> UpdateApplication;

        /// <summary>
        /// Gets the stored applications.
        /// </summary>
        public IEnumerable<IApplicationContext> Applications => _dictionary.All;

        /// <summary>
        /// Gets the declared applications whose constructor threw, for as long as their plugin is loaded.
        /// </summary>
        public IEnumerable<ApplicationFailure> FailedApplications
        {
            get
            {
                // health probes read this on request threads while plugins may be loading
                lock (_failuresSync)
                {
                    return _failures.ToArray();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ApplicationManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:applicationmanager.initialization")
            );
        }

        /// <summary>
        /// Discovers and registers applications from the specified plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose applications are to be registered.</param>
        private void Register(IPluginContext pluginContext)
        {
            // the plugin has already been registered
            if (_dictionary.Contains(pluginContext))
            {
                return;
            }

            var assembly = pluginContext.Assembly;

            // a plugin whose applications all failed is not in the dictionary and is evaluated afresh
            DiscardFailures(pluginContext);

            foreach (var type in assembly.GetExportedTypes().Where
                (
                    x => x.IsClass &&
                    x.IsSealed &&
                    x.IsPublic &&
                    x.GetInterface(typeof(IApplication).Name) is not null
                ))
            {
                var id = type.FullName?.ToLower();
                var name = type.Name;
                var icon = string.Empty;
                var description = string.Empty;
                var contextPath = string.Empty;
                var assetPath = "./";
                var dataPath = "./";
                Type defaultThemeType = null;

                // determining attributes
                foreach (var customAttribute in type.CustomAttributes
                    .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(IApplicationAttribute))))
                {
                    if (customAttribute.AttributeType == typeof(NameAttribute))
                    {
                        name = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(IconAttribute))
                    {
                        icon = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DescriptionAttribute))
                    {
                        description = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(ContextPathAttribute))
                    {
                        contextPath = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(AssetPathAttribute))
                    {
                        assetPath = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DataPathAttribute))
                    {
                        dataPath = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType.IsGenericType &&
                        customAttribute.AttributeType.GetGenericTypeDefinition() == typeof(ThemeAttribute<>))
                    {
                        // [Theme<TTheme>] declares the application's default theme.
                        defaultThemeType ??= customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();
                    }
                }

                // creating application context
                var applicationContext = new ApplicationContext
                {
                    PluginContext = pluginContext,
                    ApplicationId = id,
                    ApplicationName = name,
                    Description = description,
                    ContextPath = contextPath,
                    AssetPath = Path.Combine(_httpServerContext?.AssetPath, assetPath),
                    DataPath = Path.Combine(_httpServerContext?.DataPath, dataPath),
                    Icon = RouteEndpoint.Combine(_httpServerContext?.Route, contextPath, icon),
                    Route = RouteEndpoint.Combine(_httpServerContext?.Route, contextPath),
                    DefaultThemeType = defaultThemeType
                };

                IApplication applicationInstance;

                try
                {
                    applicationInstance = ComponentActivator.CreateInstance<IApplication, IApplicationContext>
                    (
                        type,
                        applicationContext,
                        _httpServerContext,
                        _componentHub
                    );
                }
                catch (Exception ex)
                {
                    // an escaping exception would also abort the plugin's remaining applications
                    // and every later subscriber of the AddPlugin event, health discovery included
                    var cause = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;

                    lock (_failuresSync)
                    {
                        _failures.Add(new ApplicationFailure
                        {
                            ApplicationId = id,
                            PluginContext = pluginContext,
                            Exception = cause
                        });
                    }

                    _httpServerContext?.Log?.Error
                    (
                        I18N.Translate("webexpress.webcore:applicationmanager.application.failed", id)
                    );
                    _httpServerContext?.Log?.Exception(cause);

                    continue;
                }

                if (_dictionary.AddApplication(pluginContext, new ApplicationItem()
                {
                    ApplicationClass = type,
                    ApplicationContext = applicationContext,
                    Application = applicationInstance,
                    DeclaredApplicationName = name,
                    DeclaredIcon = icon
                }))
                {
                    _httpServerContext?.Log?.Debug
                    (
                        I18N.Translate("webexpress.webcore:applicationmanager.register", id)
                    );

                    // raises the AddApplication event
                    OnAddApplication(applicationContext);
                }
                else
                {
                    _httpServerContext?.Log?.Warning
                    (
                        I18N.Translate("webexpress.webcore:applicationmanager.duplicate", id)
                    );
                }
            }

            Log();
        }

        /// <summary>
        /// Removes all applications associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the applications to remove.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            DiscardFailures(pluginContext);

            foreach (var applicationContext in _dictionary.RemoveApplications(pluginContext))
            {
                OnRemoveApplication(applicationContext);
            }

            Log();
        }

        /// <summary>
        /// Forgets the failed applications of a plugin, whose code is either gone or about to be evaluated again.
        /// </summary>
        /// <param name="pluginContext">The plugin that declares the failed applications.</param>
        private void DiscardFailures(IPluginContext pluginContext)
        {
            lock (_failuresSync)
            {
                _failures.RemoveAll(x => x.PluginContext == pluginContext);
            }
        }

        /// <summary>
        /// Returns the application context for a given application id.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <returns>The context of the application or null if the application id is null, empty, or not found.</returns>
        public IApplicationContext GetApplication(string applicationId)
        {
            return _dictionary.GetApplication(applicationId);
        }

        /// <summary>
        /// Returns the application contexts for a given application id.
        /// </summary>
        /// <typeparam name="T">The application type.</typeparam>
        /// <returns>The context of the application or null.</returns>
        public IApplicationContext GetApplication<T>()
        {
            return GetApplications(typeof(T)).FirstOrDefault();
        }

        /// <summary>
        /// Returns the application contexts for the given application ids.
        /// </summary>
        /// <param name="applicationIds">The applications ids. Can contain regular expressions or * for all.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        public IEnumerable<IApplicationContext> GetApplications(IEnumerable<string> applicationIds)
        {
            var list = new List<IApplicationContext>();

            foreach (var applicationId in applicationIds)
            {
                if (applicationId == "*")
                {
                    list.AddRange(Applications);
                }
                else
                {
                    list.AddRange
                    (
                        Applications.Where
                        (
                            x =>
                            x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase) ||
                            Regex.Match(x.ApplicationId, applicationId).Success
                        )
                    );
                }
            }

            return list.Distinct();
        }

        /// <summary>
        /// Returns the application contexts for the given plugin.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        public IEnumerable<IApplicationContext> GetApplications(IPluginContext pluginContext)
        {
            return _dictionary.GetApplications(pluginContext);
        }

        /// <summary>
        /// Returns the application contexts for a given application type.
        /// </summary>
        /// <param name="application">The application type.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        public IEnumerable<IApplicationContext> GetApplications(Type application)
        {
            return _dictionary.GetApplications(application);
        }

        /// <summary>
        /// Replaces the display name of a registered application.
        /// </summary>
        /// <param name="applicationContext">The context of the application to rename.</param>
        /// <param name="applicationName">The new name. A blank value restores the declared one.</param>
        public void SetApplicationName(IApplicationContext applicationContext, string applicationName)
        {
            var item = _dictionary.GetApplicationItem(applicationContext);

            if (item?.ApplicationContext is not ApplicationContext context)
            {
                return;
            }

            var name = string.IsNullOrWhiteSpace(applicationName)
                ? item.DeclaredApplicationName
                : applicationName;

            if (context.ApplicationName == name)
            {
                return;
            }

            context.ApplicationName = name;

            OnUpdateApplication(context);
        }

        /// <summary>
        /// Replaces the icon of a registered application.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="icon">The new icon path, relative to the application. A blank value
        /// restores the declared one.</param>
        public void SetApplicationIcon(IApplicationContext applicationContext, string icon)
        {
            var item = _dictionary.GetApplicationItem(applicationContext);

            if (item?.ApplicationContext is not ApplicationContext context)
            {
                return;
            }

            var path = string.IsNullOrWhiteSpace(icon) ? item.DeclaredIcon : icon;

            // combined exactly as at registration, so a caller hands over the same relative path
            // the [Icon] attribute would have carried and never has to assemble a route
            var route = RouteEndpoint.Combine(_httpServerContext?.Route, context.ContextPath, path);

            if (context.Icon?.ToString() == route?.ToString())
            {
                return;
            }

            context.Icon = route;

            OnUpdateApplication(context);
        }

        /// <summary>
        /// Raises the update event and logs the change.
        /// </summary>
        /// <param name="applicationContext">The context that changed.</param>
        private void OnUpdateApplication(IApplicationContext applicationContext)
        {
            UpdateApplication?.Invoke(this, applicationContext);

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:applicationmanager.update", applicationContext.ApplicationId)
            );
        }

        /// <summary>
        /// Boots the applications.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the applications.</param>
        public void Boot(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }
            else if (pluginContext.Assembly.GetCustomAttribute<SystemPluginAttribute>() is not null)
            {
                return;
            }
            else if (!_dictionary.Contains(pluginContext))
            {
                _httpServerContext?.Log?.Warning
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:applicationmanager.application.boot.notfound",
                        pluginContext.PluginId
                    )
                );

                return;
            }

            foreach (var applicationItem in _dictionary.GetApplicationItems(pluginContext))
            {
                var token = applicationItem.CancellationTokenSource.Token;

                _httpServerContext.Lifetime.TryRun(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    _httpServerContext?.Log?.Debug
                    (
                        I18N.Translate
                        (
                            "webexpress.webcore:applicationmanager.application.processing.start",
                            applicationItem.ApplicationContext.ApplicationId)
                        );

                    applicationItem.Application.Run();

                    _httpServerContext?.Log?.Debug
                    (
                        I18N.Translate
                        (
                            "webexpress.webcore:applicationmanager.application.processing.end",
                            applicationItem.ApplicationContext.ApplicationId
                        )
                    );

                });
            }
        }

        /// <summary>
        /// Shutting down applications.
        /// </summary>
        ///  <param name="pluginContext">The context of the plugin that contains the applications.</param>
        public void ShutDown(IPluginContext pluginContext)
        {
            foreach (var applicationItem in _dictionary.GetApplicationItems(pluginContext))
            {
                applicationItem.CancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Raises the AddApplication event.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        private void OnAddApplication(IApplicationContext applicationContext)
        {
            AddApplication?.Invoke(this, applicationContext);
        }

        /// <summary>
        /// Raises the RemoveApplication event.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        private void OnRemoveApplication(IApplicationContext applicationContext)
        {
            RemoveApplication?.Invoke(this, applicationContext);
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
        /// Information about the component is collected and prepared for output in the log.
        /// </summary>
        private void Log()
        {
            if (!Applications.Any())
            {
                return;
            }

            using var frame = new LogFrameSimple(_httpServerContext?.Log);
            var list = new List<string>
            {
                I18N.Translate("webexpress.webcore:applicationmanager.titel")
            };

            foreach (var applicationContext in Applications)
            {
                list.Add
                (
                    string.Empty.PadRight(2) +
                    I18N.Translate("webexpress.webcore:applicationmanager.application", applicationContext.ApplicationId)
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

            foreach (var context in _dictionary.All.ToArray())
            {
                var item = _dictionary.GetApplicationItem(context);
                try
                {
                    item.CancellationTokenSource.Cancel();
                    item.Application?.Dispose();
                    item.CancellationTokenSource.Dispose();
                }
                catch (Exception ex)
                {
                    _httpServerContext?.Log?.Exception(ex);
                }
            }
        }
    }
}
