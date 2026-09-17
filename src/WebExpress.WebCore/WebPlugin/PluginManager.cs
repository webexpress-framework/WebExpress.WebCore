using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPlugin.Model;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Central registry for plugins. It loads plugin assemblies, resolves the dependencies between
    /// them, activates them in the right order, tracks their runtime state, and unloads them again.
    /// This is the backbone of WebExpress's plugin system.
    /// </summary>
    public sealed class PluginManager : IPluginManager, IExecutableElements, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly PluginDictionary _dictionary = [];
        private readonly PluginDictionary _unfulfilledDependencies = [];

        /// <summary>
        /// An event that fires when an plugin is added.
        /// </summary>
        public event EventHandler<IPluginContext> AddPlugin;

        /// <summary>
        /// An event that fires when an plugin is removed.
        /// </summary>
        public event EventHandler<IPluginContext> RemovePlugin;

        /// <summary>
        /// Gets all plugins.
        /// </summary>
        public IEnumerable<IPluginContext> Plugins => _dictionary.Values.Select(x => x.PluginContext).ToList();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private PluginManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:pluginmanager.initialization")
            );
        }

        /// <summary>
        /// Loads and registers the plugins that are static (i.e. located in the application's folder).
        /// </summary>1
        /// <returns>A list of plugins created.</returns>
        internal void Register()
        {
            // the statically deployed plugins sit next to the host assembly. the working
            // directory is whatever the process happened to be started from - a service
            // launched from the system directory would find no plugin at all
            var path = AppContext.BaseDirectory;
            var assemblies = new List<Assembly>();

            // create plugins
            foreach (var assemblyFile in Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyFile);
                    if (assembly is not null)
                    {
                        assemblies.Add(assembly);
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:pluginmanager.load",
                                assembly.GetName().Name,
                                assembly.GetName().Version.ToString()
                            )
                        );
                    }
                }
                catch (BadImageFormatException)
                {

                }
            }

            // register plugin
            foreach (var assembly in assemblies.OrderBy(x => x.GetCustomAttribute<SystemPluginAttribute>() is not null
                ? 0
                : 1))
            {
                Register(assembly);
            }

            Log();
        }

        /// <summary>
        /// Loads and registers the plugins from a path.
        /// </summary>
        /// <param name="pluginFile">The directory and filename where the plugins are located.</param>
        internal IEnumerable<IPluginContext> Register(string pluginFile)
        {
            var assemblies = new List<Assembly>();
            var pluginContexts = new List<IPluginContext>();

            if (!File.Exists(pluginFile))
            {
                return pluginContexts;
            }

            var loadContext = new PluginLoadContext(pluginFile);

            // create plugins
            try
            {
                var assembly = loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(pluginFile));

                if (assembly is not null)
                {
                    assemblies.Add(assembly);
                    _httpServerContext?.Log?.Debug
                    (
                        I18N.Translate
                        (
                            "webexpress.webcore:pluginmanager.load",
                            assembly.GetName().Name,
                            assembly.GetName().Version.ToString()
                        )
                    );
                }
            }
            catch (BadImageFormatException)
            {

            }

            // register plugin
            foreach (var assembly in assemblies)
            {
                var pluginContext = Register(assembly, loadContext);
                pluginContexts.AddRange(pluginContext);
            }

            Log();

            return pluginContexts;
        }

        /// <summary>
        /// Loads and registers the plugin from an assembly.
        /// </summary>
        /// <param name="assembly">The assembly where the plugin is located.</param>
        /// <param name="loadContext">The plugin load context for isolating and unloading the dependent libraries.</param>
        /// <returns>A collection of created plugin contexts.</returns>
        private IEnumerable<IPluginContext> Register(Assembly assembly, PluginLoadContext loadContext = null)
        {
            var plugins = new List<IPluginContext>();

            try
            {
                // system plugins without plugin class (e.g. webexpress.webui)
                if (assembly.GetCustomAttribute<SystemPluginAttribute>() is not null)
                {
                    var attributeData = assembly.CustomAttributes
                        .FirstOrDefault(a => a.AttributeType == typeof(SystemPluginAttribute));
                    var dependency = attributeData.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    var dependencies = dependency is not null
                        ? new List<string>([dependency])
                        : [];
                    var id = new ComponentId(assembly.GetName().Name.ToLower());
                    var pluginContext = new PluginContext()
                    {
                        Assembly = assembly,
                        PluginId = id,
                        PluginName = assembly.GetName().Name.ToLower(),
                        Manufacturer = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company,
                        Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright,
                        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                        Settings = _httpServerContext?.Configuration?.GetPluginSettings(id)
                    };

                    var hasUnfulfilledDependencies = HasUnfulfilledDependencies(id, dependencies.Select(x => new ComponentId(x)));

                    if (!_dictionary.ContainsKey(id))
                    {
                        if (hasUnfulfilledDependencies)
                        {
                            _unfulfilledDependencies.Add(id, new PluginItem()
                            {
                                PluginLoadContext = loadContext,
                                PluginClass = assembly.ExportedTypes.FirstOrDefault() ?? typeof(IPlugin),
                                PluginContext = pluginContext,
                                Plugin = null,
                                Dependencies = dependencies,
                                ApplicationTypes = [typeof(IApplication)]
                            });
                        }
                        else if (!_dictionary.ContainsKey(id))
                        {
                            _dictionary.Add(id, new PluginItem()
                            {
                                PluginLoadContext = loadContext,
                                PluginClass = assembly.ExportedTypes.FirstOrDefault() ?? typeof(IPlugin),
                                PluginContext = pluginContext,
                                Plugin = null,
                                Dependencies = dependencies,
                                ApplicationTypes = [typeof(IApplication)]
                            });

                            _httpServerContext?.Log?.Debug
                            (
                                I18N.Translate("webexpress.webcore:pluginmanager.created", id)
                            );

                            OnAddPlugin(pluginContext);

                            CheckUnfulfilledDependencies();
                        }
                    }
                    else
                    {
                        _httpServerContext?.Log?.Warning
                        (
                            I18N.Translate("webexpress.webcore:pluginmanager.duplicate", id)
                        );
                    }

                    plugins.Add(pluginContext);
                }

                foreach (var type in assembly
                    .GetExportedTypes()
                    .Where(x => x.IsClass && x.IsSealed)
                    .Where(x => x.GetInterface(typeof(IPlugin).Name) is not null))
                {
                    var id = new ComponentId(type.Namespace);
                    var name = type.Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
                    var icon = string.Empty;
                    var description = type.Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
                    var dependencies = new List<string>();
                    var hasUnfulfilledDependencies = false;
                    var applicationTypes = new List<Type>();

                    foreach (var customAttribute in type.CustomAttributes
                        .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(IPluginAttribute))))
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
                        else if (customAttribute.AttributeType == typeof(DependencyAttribute))
                        {
                            dependencies.Add(customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString());
                        }
                        else if (customAttribute.AttributeType.Name == typeof(ApplicationAttribute<>).Name && customAttribute.AttributeType.Namespace == typeof(ApplicationAttribute<>).Namespace)
                        {
                            applicationTypes.Add(customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault());
                        }
                    }

                    if (plugins.Count > 0)
                    {
                        // to many plugins, only one per assembly
                        _httpServerContext?.Log?.Warning
                        (
                            I18N.Translate("webexpress.webcore:pluginmanager.tomany", type.FullName)
                        );

                        break;
                    }

                    if (applicationTypes.Count == 0)
                    {
                        // no application specified
                        _httpServerContext?.Log?.Warning
                        (
                            I18N.Translate("webexpress.webcore:pluginmanager.applicationless", id)
                        );

                        break;
                    }

                    var pluginContext = new PluginContext()
                    {
                        Assembly = type.Assembly,
                        PluginId = id,
                        PluginName = name,
                        Manufacturer = type.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company,
                        Copyright = type.Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright,
                        // a plugin without an icon attribute has no icon: combining the empty
                        // value would yield the server route, which callers cannot tell apart
                        // from a real icon and would render as a broken image
                        Icon = !string.IsNullOrWhiteSpace(icon)
                            ? RouteEndpoint.Combine(_httpServerContext?.Route, icon)
                            : null,
                        Description = description,
                        Version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                        Settings = _httpServerContext?.Configuration?.GetPluginSettings(id)
                    };

                    hasUnfulfilledDependencies = HasUnfulfilledDependencies(id, dependencies.Select(x => new ComponentId(x)));

                    if (hasUnfulfilledDependencies)
                    {
                        _unfulfilledDependencies.Add(id, new PluginItem()
                        {
                            PluginLoadContext = loadContext,
                            PluginClass = type,
                            PluginContext = pluginContext,
                            Plugin = ComponentActivator.CreateInstance<IPlugin, IPluginContext>(type, pluginContext, _httpServerContext, _componentHub),
                            Dependencies = dependencies
                        });
                    }
                    else if (!_dictionary.ContainsKey(id))
                    {
                        _dictionary.Add(id, new PluginItem()
                        {
                            PluginLoadContext = loadContext,
                            PluginClass = type,
                            PluginContext = pluginContext,
                            Plugin = ComponentActivator.CreateInstance<IPlugin, IPluginContext>(type, pluginContext, _httpServerContext, _componentHub),
                            Dependencies = dependencies,
                            ApplicationTypes = applicationTypes
                        });

                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate("webexpress.webcore:pluginmanager.created", id)
                        );

                        OnAddPlugin(pluginContext);

                        CheckUnfulfilledDependencies();
                    }
                    else
                    {
                        _httpServerContext?.Log?.Warning
                        (
                            I18N.Translate("webexpress.webcore:pluginmanager.duplicate", id)
                        );
                    }

                    plugins.Add(pluginContext);
                }
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }

            return plugins;
        }

        /// <summary>
        /// Removes all elemets associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the elemets to remove.</param>
        public void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            OnRemovePlugin(pluginContext);

            var pluginItem = GetPluginItem(pluginContext);
            pluginItem?.PluginLoadContext?.Unload();

            _dictionary.Remove(pluginContext.PluginId);
        }

        /// <summary>
        /// Check if dependencies of other plugins are now fulfilled after a plugin has been added.
        /// </summary>
        private void CheckUnfulfilledDependencies()
        {
            bool fulfilledDependencies;

            do
            {
                fulfilledDependencies = false;

                foreach (var unfulfilledDependencies in _unfulfilledDependencies)
                {
                    var hasUnfulfilledDependencies = HasUnfulfilledDependencies
                    (
                        unfulfilledDependencies.Key,
                        unfulfilledDependencies.Value.Dependencies.Select(x => new ComponentId(x))
                    );

                    if (!hasUnfulfilledDependencies)
                    {
                        fulfilledDependencies = true;
                        _unfulfilledDependencies.Remove(unfulfilledDependencies.Key);
                        _dictionary.Add(unfulfilledDependencies.Key, unfulfilledDependencies.Value);

                        OnAddPlugin(unfulfilledDependencies.Value.PluginContext);

                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:pluginmanager.fulfilleddependencies",
                                unfulfilledDependencies.Key
                            )
                        );
                    }
                }
            } while (fulfilledDependencies);
        }

        /// <summary>
        /// Checks if there are any unfulfilled dependencies.
        /// </summary>
        /// <param name="id">The id of the plugin.</param>
        /// <param name="dependencies">The dependencies to check.</param>
        /// <returns>True if dependencies exist, false otherwise</returns>
        private bool HasUnfulfilledDependencies(IComponentId id, IEnumerable<IComponentId> dependencies)
        {
            var hasUnfulfilledDependencies = false;

            foreach (var dependency in dependencies
                   .Where(x => !_dictionary.ContainsKey(x)))
            {
                // dependency was not fulfilled
                hasUnfulfilledDependencies = true;

                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:pluginmanager.unfulfilleddependencies",
                        id,
                        dependency
                    )
                );
            }

            return hasUnfulfilledDependencies;
        }

        /// <summary>
        /// Returns a plugin context based on its id.
        /// </summary>
        /// <param name="pluginId">The id of the plugin.</param>
        /// <returns>The plugin context.</returns>
        public IPluginContext GetPlugin(string pluginId)
        {
            return _dictionary.Values
                .Where
                (
                    x => x.PluginContext is not null &&
                    x.PluginContext.PluginId.ToString().Equals(pluginId)
                )
                .Select(x => x.PluginContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a plugin context based on its id.
        /// </summary>
        /// <param name="plugin">The type of the plugin.</param>
        /// <returns>The plugin context.</returns>
        public IPluginContext GetPlugin(Type plugin)
        {
            return _dictionary.Values
                .Where
                (
                    x => x.PluginContext is not null &&
                    x.PluginClass.Equals(plugin)
                )
                .Select(x => x.PluginContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns all plugins that have associated applications.
        /// </summary>
        /// <param name="applicationContext">The application context to filter plugins.</param>
        /// <returns>An enumerable collection of plugin contexts with applications.</returns>
        public IEnumerable<IPluginContext> GetPlugins(IApplicationContext applicationContext)
        {
            return _dictionary.Values
                .Where(x => x.ApplicationTypes is not null)
                .Where(x => x.ApplicationTypes.Select(x => _componentHub?.ApplicationManager.GetApplications(x))
                .SelectMany(x => x)
                .Where(x => x.ApplicationId == applicationContext.ApplicationId)
                .Any())
                .Select(x => x.PluginContext);
        }

        /// <summary>
        /// Returns all ApplicationContext instances associated with a plugin.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin.</param>
        /// <returns>A collection of ApplicationContext instances.</returns>
        public IEnumerable<IApplicationContext> GetAssociatedApplications(IPluginContext pluginContext)
        {
            var pluginItem = GetPluginItem(pluginContext);

            if (pluginItem is null)
            {
                return [];
            }

            return pluginItem.ApplicationTypes?
                .Select(x => _componentHub?.ApplicationManager.GetApplications(x))
                .SelectMany(x => x)
                .Where(x => x is not null) ?? [];
        }

        /// <summary>
        /// Gets runtime metadata for all known plugins, including dependency and status information.
        /// </summary>
        /// <returns>A list of plugin runtime metadata entries.</returns>
        public IEnumerable<PluginRuntimeInfo> GetPluginRuntimeInfos()
        {
            var active = _dictionary.Values
                .Where(x => x?.PluginContext is not null)
                .Select(x => new PluginRuntimeInfo()
                {
                    PluginContext = x.PluginContext,
                    Dependencies = x.Dependencies ?? [],
                    State = PluginRuntimeState.Active
                });

            var waiting = _unfulfilledDependencies.Values
                .Where(x => x?.PluginContext is not null)
                .Select(x => new PluginRuntimeInfo()
                {
                    PluginContext = x.PluginContext,
                    Dependencies = x.Dependencies ?? [],
                    State = PluginRuntimeState.WaitingForDependencies
                });

            return [.. active.Concat(waiting)];
        }


        /// <summary>
        /// Returns a plugin item based on the context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin.</param>
        /// <returns>The plugin item or null.</returns>
        private PluginItem GetPluginItem(IPluginContext pluginContext)
        {
            var pluginId = pluginContext?.PluginId;

            if (pluginId is null || !_dictionary.TryGetValue(pluginId, out PluginItem value))
            {
                _httpServerContext?.Log?.Warning
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:pluginmanager.notavailable",
                        pluginId
                    )
                );

                return null;
            }

            return value;
        }

        /// <summary>
        /// Boots the specified plugin.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin to run.</param>
        internal void Boot(IPluginContext pluginContext)
        {
            var pluginItem = GetPluginItem(pluginContext);
            var token = pluginItem?.CancellationTokenSource.Token;

            if (pluginItem is null)
            {
                return;
            }

            if (pluginItem.Plugin is null)
            {
                return;
            }

            // run plugin concurrently
            Task.Run(() =>
            {
                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:pluginmanager.plugin.processing.start",
                        pluginItem.PluginContext.PluginId
                    )
                );

                pluginItem.Plugin.Run();

                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:pluginmanager.plugin.processing.end",
                        pluginItem.PluginContext.PluginId
                    )
                );

                token?.ThrowIfCancellationRequested();
            }, token.Value);
        }

        /// <summary>
        /// Boots the specified plugins.
        /// </summary>
        /// <param name="contexts">A list with the contexts of the plugins to run.</param>
        internal void Boot(IEnumerable<IPluginContext> contexts)
        {
            foreach (var context in contexts)
            {
                Boot(context);
            }
        }

        /// <summary>
        /// Shut down the plugin.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin to shut down.</param>
        internal void ShutDown(IPluginContext pluginContext)
        {
            var plugin = GetPluginItem(pluginContext);

            plugin?.CancellationTokenSource.Cancel();

        }

        /// <summary>
        /// Shut down the plugins.
        /// </summary>
        /// <param name="contexts">A list of contexts of plugins to shut down.</param>
        internal void ShutDown(IEnumerable<IPluginContext> contexts)
        {
            foreach (var context in contexts)
            {
                ShutDown(context);
            }
        }

        /// <summary>
        /// Raises the AddPlugin event.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        private void OnAddPlugin(IPluginContext pluginContext)
        {
            AddPlugin?.Invoke(this, pluginContext);
        }

        /// <summary>
        /// Raises the RemovePlugin event.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        private void OnRemovePlugin(IPluginContext pluginContext)
        {
            RemovePlugin?.Invoke(this, pluginContext);
        }

        /// <summary>
        /// Output of the loaded plugins to the log.
        /// </summary>
        private void Log()
        {
            using var frame = new LogFrameSimple(_httpServerContext?.Log);
            var list = new List<string>();
            _httpServerContext?.Log?.Info
            (
                I18N.Translate
                (
                    "webexpress.webcore:pluginmanager.pluginmanager.label"
                )
            );

            list.AddRange(_dictionary
                .Where
                (
                    x => x.Value.PluginClass.Assembly
                        .GetCustomAttribute<SystemPluginAttribute>() is not null
                )
                .Select(x => string.Empty.PadRight(2) + I18N.Translate
                (
                    "webexpress.webcore:pluginmanager.pluginmanager.system",
                    x.Key
                ))
            );

            list.AddRange(_dictionary
                .Where
                (
                    x => x.Value.PluginClass.Assembly
                        .GetCustomAttribute<SystemPluginAttribute>() is null
                )
                .Select(x => string.Empty.PadRight(2) + I18N.Translate
                (
                    "webexpress.webcore:pluginmanager.pluginmanager.custom",
                    x.Key
                ))
            );

            list.AddRange(_unfulfilledDependencies
                .Select(x => string.Empty.PadRight(2) + I18N.Translate
                (
                    "webexpress.webcore:pluginmanager.pluginmanager.unfulfilleddependencies",
                    x.Key
                ))
            );

            foreach (var item in list)
            {
                _httpServerContext?.Log?.Info(string.Join(Environment.NewLine, item));
            }
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
