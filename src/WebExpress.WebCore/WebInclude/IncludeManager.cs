using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebInclude.Model;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebInclude
{
    /// <summary>
    /// The include manager discovers and manages JavaScript and CSS include resources on a per-plugin and per-application basis.
    /// It raises events upon add/remove and maintains a context view for consumers (e.g., endpoints that serve bundled assets).
    /// </summary>
    public sealed class IncludeManager : IIncludeManager, ISystemComponent, IDisposable
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly IncludeDictionary _dictionary = new();
        private readonly ConcurrentDictionary<IPluginContext, (HashSet<string> Declared, HashSet<string> Referenced)> _dependencies = new();

        /// <summary>
        /// An event that fires when an include is added.
        /// </summary>
        public event EventHandler<IIncludeContext> AddInclude;

        /// <summary>
        /// An event that fires when an include is removed.
        /// </summary>
        public event EventHandler<IIncludeContext> RemoveInclude;

        /// <summary>
        /// Gets all include contexts.
        /// </summary>
        public IEnumerable<IIncludeContext> Includes => [.. _dictionary.All.Select(x => x.IncludeContext)];

        /// <summary>
        /// Initializes a new instance of the include manager.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private IncludeManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:includemanager.initialization")
            );
        }

        /// <summary>
        /// Discovers and binds includes for all applications associated with the plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            if (_dictionary.Contains(pluginContext))
            {
                return;
            }

            Register(pluginContext, _componentHub.GetBoundApplications(pluginContext));
        }

        /// <summary>
        /// Discovers and binds includes for a single application across all its plugins.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        private void Register(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

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
        /// Registers includes for a given plugin and application contexts.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application contexts.</param>
        private void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var assembly = pluginContext?.Assembly;

            if (assembly is null)
            {
                return;
            }

            // the asset manager subscribes to the plugin and application events before this
            // manager does, so every asset of the plugin is already mounted by the time an
            // include is registered and a file that resolves to nothing here would render a
            // link that answers 404 at runtime. neither the page nor the browser reports
            // that - the html error page is accepted as a stylesheet with no rules - so
            // registration is the only moment the dead file can still be named.
            var mountedRoutes = new Dictionary<IApplicationContext, HashSet<string>>();

            foreach (var includeType in assembly.GetTypes()
                .Where(x => x.IsClass && x.IsSealed && x.IsPublic)
                .Where(x => x.GetInterface(typeof(IInclude).Name) is not null))
            {
                var id = includeType.FullName?.ToLowerInvariant();
                var cache = false;
                var scopes = new List<Type>();
                var files = new List<(TypeInclude, string)>();

                foreach (var customAttribute in includeType.CustomAttributes
                    .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(IIncludeAttribute))))
                {
                    if (customAttribute.AttributeType == typeof(CacheAttribute))
                    {
                        cache = true;
                    }
                    else if (customAttribute.AttributeType.Name == typeof(ScopeAttribute<>).Name &&
                        customAttribute.AttributeType.Namespace == typeof(ScopeAttribute<>).Namespace)
                    {
                        scopes.Add(customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault());
                    }
                    else if (customAttribute.AttributeType == typeof(AssetAttribute))
                    {
                        var file = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                        var extension = Path.GetExtension(file);

                        switch (extension)
                        {
                            case ".js":
                                files.Add((TypeInclude.JavaScript, file));
                                break;
                            case ".css":
                                files.Add((TypeInclude.StyleSheet, file));
                                break;
                            default:
                                break;
                        }
                    }
                }

                // assign the resource to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var includeContext = new IncludeContext()
                    {
                        IncludeId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        Cache = cache,
                        Scopes = scopes,
                        Files = files.Select(x => new IncludeFile() { Type = x.Item1, FileName = x.Item2 })
                    };

                    var includeItem = new IncludeItem(_componentHub)
                    {
                        IncludeId = includeContext.IncludeId,
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        IncludeContext = includeContext,
                        IncludeClass = includeType,
                        Cache = cache,
                        Scopes = scopes,
                        Files = files.Select(x => new IncludeFile() { Type = x.Item1, FileName = x.Item2 })
                    };

                    WarnAboutUnresolvedFiles(includeContext, mountedRoutes);

                    if (_dictionary.AddIncludeItem(pluginContext, applicationContext, includeItem))
                    {
                        OnAddInclude(includeItem.IncludeContext);

                        _httpServerContext?.Log?.Debug(
                            I18N.Translate(
                                "webexpress.webcore:includemanager.addinclude",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                }

            }
        }

        /// <summary>
        /// Logs every file of an include that does not resolve to an asset the asset manager
        /// has mounted. The include is kept rather than refused, because the two managers are
        /// wired to the same events and a hard refusal would turn any future change of that
        /// order into a failed start instead of a log line.
        /// </summary>
        /// <param name="includeContext">The include whose files are checked.</param>
        /// <param name="mountedRoutes">The per-application index of mounted asset routes, filled on demand.</param>
        private void WarnAboutUnresolvedFiles(IIncludeContext includeContext, Dictionary<IApplicationContext, HashSet<string>> mountedRoutes)
        {
            var applicationContext = includeContext.ApplicationContext;
            var assetManager = _componentHub?.AssetManager;

            if (assetManager is null)
            {
                return;
            }

            if (!mountedRoutes.TryGetValue(applicationContext, out var mounted))
            {
                mounted = assetManager.GetAssets(applicationContext)
                    .Select(x => x.Route?.ToString())
                    .Where(x => x is not null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                mountedRoutes[applicationContext] = mounted;
            }

            foreach (var file in includeContext.Files)
            {
                var route = assetManager.GetAssetRoute
                (
                    applicationContext,
                    includeContext.PluginContext,
                    file.FileName
                )?.ToString();

                if (route is not null && mounted.Contains(route))
                {
                    continue;
                }

                _httpServerContext?.Log?.Warning(
                    I18N.Translate(
                        "webexpress.webcore:includemanager.unresolvedfile",
                        includeContext.IncludeId,
                        file.FileName,
                        route ?? string.Empty,
                        applicationContext.ApplicationId
                    )
                );
            }
        }

        /// <summary>
        /// Removes all includes associated with the plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            // a plugin loaded again may come with other dependencies
            _dependencies.TryRemove(pluginContext, out _);

            // removed first and announced afterwards, outside the registry's lock
            foreach (var includeItem in _dictionary.Remove(pluginContext))
            {
                OnRemoveInclude(includeItem.IncludeContext);
                includeItem.Dispose();

                _httpServerContext?.Log?.Debug(
                    I18N.Translate(
                        "webexpress.webcore:includemanager.removeinclude",
                        includeItem.IncludeId,
                        includeItem.ApplicationContext.ApplicationId
                    )
                );
            }
        }

        /// <summary>
        /// Removes all includes associated with the application context.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

            foreach (var includeItem in _dictionary.Remove(applicationContext))
            {
                OnRemoveInclude(includeItem.IncludeContext);
                includeItem.Dispose();

                _httpServerContext?.Log?.Debug(
                    I18N.Translate(
                        "webexpress.webcore:includemanager.removeinclude",
                        includeItem.IncludeId,
                        includeItem.ApplicationContext.ApplicationId
                    )
                );
            }
        }

        /// <summary>
        /// Returns include contexts for a given application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>Enumerable of include contexts.</returns>
        public IEnumerable<IIncludeContext> GetIncludes(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return [];
            }

            return [.. OrderByDependency(_dictionary.GetItems(applicationContext)).Select(x => x.IncludeContext)];
        }

        /// <summary>
        /// Returns include contexts for a given application and include type.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="includeType">The include class type.</param>
        /// <returns>Enumerable of include contexts.</returns>
        public IEnumerable<IIncludeContext> GetIncludes(IApplicationContext applicationContext, Type includeType)
        {
            if (applicationContext is null || includeType is null)
            {
                return [];
            }

            return [.. OrderByDependency(_dictionary.GetItems(applicationContext, includeType)).Select(x => x.IncludeContext)];
        }

        /// <summary>
        /// Orders include items so that the includes of a plugin follow those of every plugin it
        /// builds on. A script extends the classes of the scripts before it and a stylesheet
        /// overrides the rules before it, so the includes of a plugin must never be linked ahead
        /// of those of its dependencies. The registry cannot provide that order itself: the
        /// plugins are enumerated in hash order, which changes with every start of the server.
        /// </summary>
        /// <remarks>
        /// A plugin builds on another when it declares it as a dependency or when its assembly
        /// references the other plugin's assembly - most plugins only do the latter. Plugins
        /// without such a relation are ordered by their id, and a cycle is broken in the same
        /// order, so the result is the same on every start. The includes of one plugin keep
        /// their registration order.
        /// </remarks>
        /// <param name="items">The include items to order.</param>
        /// <returns>The include items in the order they are to be linked.</returns>
        private IEnumerable<IncludeItem> OrderByDependency(IReadOnlyList<IncludeItem> items)
        {
            var byPlugin = items
                .GroupBy(x => x.PluginContext)
                .OrderBy(x => x.Key.PluginId?.ToString(), StringComparer.Ordinal)
                .ToList();

            if (byPlugin.Count < 2)
            {
                return items;
            }

            var assemblyNames = byPlugin.ToDictionary
            (
                x => x.Key,
                x => x.Key.Assembly?.GetName().Name ?? string.Empty
            );

            var pending = byPlugin;
            var ordered = new List<IncludeItem>(items.Count);

            while (pending.Count > 0)
            {
                // a plugin is due once none of the plugins still pending is one it builds on
                var next = pending.FirstOrDefault(x => pending
                    .Where(other => other != x)
                    .All(other => !DependsOn(x.Key, other.Key, assemblyNames[other.Key])))
                    ?? pending[0];

                ordered.AddRange(next);
                pending.Remove(next);
            }

            return ordered;
        }

        /// <summary>
        /// Determines whether a plugin builds on another plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin that may depend on the other.</param>
        /// <param name="other">The plugin that may be a dependency.</param>
        /// <param name="otherAssemblyName">The name of the other plugin's assembly.</param>
        /// <returns>True if the plugin declares or references the other plugin.</returns>
        private bool DependsOn(IPluginContext pluginContext, IPluginContext other, string otherAssemblyName)
        {
            var (declared, referenced) = _dependencies.GetOrAdd(pluginContext, ResolveDependencies);

            return declared.Contains(other.PluginId?.ToString() ?? string.Empty)
                || referenced.Contains(otherAssemblyName);
        }

        /// <summary>
        /// Resolves the plugin ids a plugin declares as dependencies and the names of the
        /// assemblies its assembly references. Both are fixed for a loaded plugin, so they are
        /// resolved once instead of on every page render.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The declared plugin ids and the referenced assembly names.</returns>
        private (HashSet<string> Declared, HashSet<string> Referenced) ResolveDependencies(IPluginContext pluginContext)
        {
            var declared = ((_componentHub?.PluginManager as PluginManager)?.GetDependencies(pluginContext) ?? [])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var referenced = (pluginContext.Assembly?.GetReferencedAssemblies() ?? [])
                .Select(x => x.Name)
                .Where(x => x is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return (declared, referenced);
        }

        /// <summary>
        /// Raises the AddInclude event.
        /// </summary>
        /// <param name="includeContext">The include context.</param>
        private void OnAddInclude(IIncludeContext includeContext)
        {
            AddInclude?.Invoke(this, includeContext);
        }

        /// <summary>
        /// Raises the RemoveInclude event.
        /// </summary>
        /// <param name="includeContext">The include context.</param>
        private void OnRemoveInclude(IIncludeContext includeContext)
        {
            RemoveInclude?.Invoke(this, includeContext);
        }

        /// <summary>
        /// Handles plugin added event.
        /// </summary>
        private void OnAddPlugin(object sender, IPluginContext e)
        {
            Register(e);
        }

        /// <summary>
        /// Handles plugin removed event.
        /// </summary>
        private void OnRemovePlugin(object sender, IPluginContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Handles application removed event.
        /// </summary>
        private void OnRemoveApplication(object sender, IApplicationContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Handles application added event.
        /// </summary>
        private void OnAddApplication(object sender, IApplicationContext e)
        {
            Register(e);
        }

        /// <summary>
        /// Disposes registered handlers.
        /// </summary>
        public void Dispose()
        {
            _componentHub?.PluginManager?.AddPlugin -= OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin -= OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication -= OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication -= OnRemoveApplication;
        }
    }
}
