using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebInclude.Model;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebSetting;

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
        private readonly bool _bundle;

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
            _bundle = httpServerContext?.Configuration?
                .GetSection(ConfigurationPath.Combine(HttpServerSettings.Section, nameof(HttpServerSettings.Assets)))
                .Get<AssetSettings>()?.Bundle ?? true;

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

            // every application bound to the plugin mounts the same files, so a bundle is joined
            // once per registration and mounted in each of them
            var bundleContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

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
                    var includeFiles = ResolveFiles(applicationContext, pluginContext, files, mountedRoutes);

                    var includeContext = new IncludeContext()
                    {
                        IncludeId = new ComponentId(id),
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        Cache = cache,
                        Scopes = scopes,
                        Files = includeFiles,
                        LinkedFiles = _bundle
                            ? BundleFiles(applicationContext, pluginContext, id, includeFiles, bundleContents)
                            : includeFiles
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
                        Files = includeFiles
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
        /// Creates the files of an include for one application, each with the route it is
        /// linked from.
        /// </summary>
        /// <remarks>
        /// Every page links every file of every include - several hundred - and composing and
        /// versioning each route on every render allocated over a thousand objects per page for
        /// a result that cannot change: the files of an include belong to its own plugin, whose
        /// assets are mounted before its includes are registered and leave together with them.
        /// A file whose asset is not mounted yet keeps no route, so the page resolves it at
        /// render time instead of linking an unversioned route for good.
        /// </remarks>
        /// <param name="applicationContext">The application the files are linked in.</param>
        /// <param name="pluginContext">The plugin the files belong to.</param>
        /// <param name="files">The type and name of each file, in declared order.</param>
        /// <param name="mountedRoutes">The per-application index of mounted asset routes, filled on demand.</param>
        /// <returns>The include files, in declared order.</returns>
        private IReadOnlyList<IncludeFile> ResolveFiles(IApplicationContext applicationContext, IPluginContext pluginContext, IEnumerable<(TypeInclude Type, string FileName)> files, Dictionary<IApplicationContext, HashSet<string>> mountedRoutes)
        {
            var assetManager = _componentHub?.AssetManager;
            var mounted = GetMountedRoutes(applicationContext, mountedRoutes);

            return [.. files.Select(x => new IncludeFile()
            {
                Type = x.Type,
                FileName = x.FileName,
                Route = mounted is not null && mounted.Contains(assetManager.GetAssetRoute(applicationContext, pluginContext, x.FileName)?.ToString() ?? string.Empty)
                    ? assetManager.GetVersionedAssetRoute(applicationContext, pluginContext, x.FileName)?.ToString()
                    : null
            })];
        }

        /// <summary>
        /// Returns the routes of the assets the asset manager has mounted for an application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="mountedRoutes">The per-application index of mounted asset routes, filled on demand.</param>
        /// <returns>The mounted routes, or null without an asset manager.</returns>
        private HashSet<string> GetMountedRoutes(IApplicationContext applicationContext, Dictionary<IApplicationContext, HashSet<string>> mountedRoutes)
        {
            var assetManager = _componentHub?.AssetManager;

            if (assetManager is null)
            {
                return null;
            }

            if (!mountedRoutes.TryGetValue(applicationContext, out var mounted))
            {
                mounted = assetManager.GetAssets(applicationContext)
                    .Select(x => x.Route?.ToString())
                    .Where(x => x is not null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                mountedRoutes[applicationContext] = mounted;
            }

            return mounted;
        }

        /// <summary>
        /// Joins the files of an include into bundles and returns what a page links instead of
        /// the single files.
        /// </summary>
        /// <remarks>
        /// The files of one type are bundled in their declared order: a script builds on the ones
        /// before it and a stylesheet overrides the rules before it. Scripts and stylesheets are
        /// loaded independently of one another, so each type forms bundles of its own. A file that
        /// is not mounted ends a bundle and keeps its place, so it is still resolved when the page
        /// renders. The order between the includes of different plugins is not affected: it is
        /// established when the includes are read.
        /// A stylesheet addresses its images relative to its own address, so stylesheets from
        /// different directories are not joined - the bundle is mounted in the directory of its
        /// files and such an address keeps pointing at the same file.
        /// </remarks>
        /// <param name="applicationContext">The application the files are linked in.</param>
        /// <param name="pluginContext">The plugin the files belong to.</param>
        /// <param name="includeId">The id of the include, which names its bundles.</param>
        /// <param name="files">The files of the include, in declared order.</param>
        /// <param name="bundleContents">The contents joined during this registration, by bundle.</param>
        /// <returns>The bundles and the files that stay single, in the order they are linked.</returns>
        private IReadOnlyList<IncludeFile> BundleFiles
        (
            IApplicationContext applicationContext,
            IPluginContext pluginContext,
            string includeId,
            IReadOnlyList<IncludeFile> files,
            Dictionary<string, byte[]> bundleContents
        )
        {
            if (_componentHub?.AssetManager is not AssetManager assetManager)
            {
                return files;
            }

            var linked = new List<(int Position, IncludeFile File)>();

            foreach (var type in files.Select(x => x.Type).Distinct())
            {
                var run = new List<(int Position, IncludeFile File)>();
                var bundleCount = 0;

                void Close()
                {
                    if (run.Count == 1)
                    {
                        linked.Add(run[0]);
                    }
                    else if (run.Count > 1)
                    {
                        var bundle = MountBundle(assetManager, applicationContext, pluginContext, includeId, type, bundleCount, [.. run.Select(x => x.File)], bundleContents);

                        if (bundle is null)
                        {
                            linked.AddRange(run);
                        }
                        else
                        {
                            linked.Add((run[0].Position, bundle));
                            bundleCount++;
                        }
                    }

                    run.Clear();
                }

                for (var position = 0; position < files.Count; position++)
                {
                    var file = files[position];

                    if (file.Type != type)
                    {
                        continue;
                    }

                    if (file.Route is null)
                    {
                        Close();
                        linked.Add((position, file));

                        continue;
                    }

                    if (run.Count > 0 && type == TypeInclude.StyleSheet &&
                        !string.Equals(GetDirectory(run[0].File.FileName), GetDirectory(file.FileName), StringComparison.OrdinalIgnoreCase))
                    {
                        Close();
                    }

                    run.Add((position, file));
                }

                Close();
            }

            return [.. linked.OrderBy(x => x.Position).Select(x => x.File)];
        }

        /// <summary>
        /// Mounts the joined content of a run of files as a bundle of the plugin.
        /// </summary>
        /// <param name="assetManager">The asset manager.</param>
        /// <param name="applicationContext">The application the bundle is mounted in.</param>
        /// <param name="pluginContext">The plugin the files belong to.</param>
        /// <param name="includeId">The id of the include, which names its bundles.</param>
        /// <param name="type">The type of the files.</param>
        /// <param name="bundleIndex">The number of bundles of this type the include has so far.</param>
        /// <param name="files">The files to join, in the order they are linked in.</param>
        /// <param name="bundleContents">The contents joined during this registration, by bundle.</param>
        /// <returns>The bundle, or null when it could not be mounted.</returns>
        private static IncludeFile MountBundle
        (
            AssetManager assetManager,
            IApplicationContext applicationContext,
            IPluginContext pluginContext,
            string includeId,
            TypeInclude type,
            int bundleIndex,
            IReadOnlyList<IncludeFile> files,
            Dictionary<string, byte[]> bundleContents
        )
        {
            var (contentType, extension) = type == TypeInclude.JavaScript
                ? (ContentType.Js, ".js")
                : (ContentType.Css, ".css");
            var suffix = bundleIndex == 0 ? string.Empty : (bundleIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"{GetDirectory(files[0].FileName)}{includeId}.bundle{suffix}{extension}";
            var key = fileName + "|" + string.Join("|", files.Select(x => x.FileName));

            if (!bundleContents.TryGetValue(key, out var content))
            {
                content = AssetBundle.Join(contentType, files.Select(x => assetManager.ReadAssetContent(applicationContext, pluginContext, x.FileName)));
                bundleContents[key] = content;
            }

            var route = assetManager.MountBundle(applicationContext, pluginContext, fileName, content);

            return route is null
                ? null
                : new IncludeFile()
                {
                    Type = type,
                    FileName = fileName,
                    Route = route.ToString()
                };
        }

        /// <summary>
        /// Returns the directory part of a file path, including the trailing separator.
        /// </summary>
        /// <param name="fileName">The file path as declared on the asset attribute.</param>
        /// <returns>The directory, empty for a file without one.</returns>
        private static string GetDirectory(string fileName)
        {
            var separator = fileName?.LastIndexOf('/') ?? -1;

            return separator < 0 ? string.Empty : fileName[..(separator + 1)];
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
            var mounted = GetMountedRoutes(applicationContext, mountedRoutes);

            if (mounted is null)
            {
                return;
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
