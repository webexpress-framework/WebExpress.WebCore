using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebCluster;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPackage.Model;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebPackage
{
    /// <summary>
    /// The package manager manages packages with WebExpress extensions. The packages
    /// must be in WebExpressPackage format (*.wxp).
    /// </summary>
    /// <remarks>
    /// A package that is neither disabled nor running is available: it waits for its
    /// dependencies or its last start failed. Every change of the package set ends with a
    /// round that starts the available packages whose dependencies are active, so neither the
    /// order of the catalog nor the order of installation matters.
    /// </remarks>
    public sealed class PackageManager : IPackageManager, ISystemComponent
    {
        private readonly ComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly PluginManager _pluginManager;

        /// <summary>
        /// An event that fires when an package is added.
        /// </summary>
        public event EventHandler<PackageCatalogItem> AddPackage;

        /// <summary>
        /// An event that fires when an package is removed.
        /// </summary>
        public event EventHandler<PackageCatalogItem> RemovePackage;

        /// <summary>
        /// Thread Termination.
        /// </summary>
        private CancellationTokenSource TokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// Gets the catalog of installed packages.
        /// </summary>
        public PackageCatalog Catalog { get; } = new PackageCatalog();

        /// <summary>
        /// Synchronization object for scanning and mutating catalog.
        /// </summary>
        private readonly Lock _scanLock = new();

        /// <summary>
        /// The topic instances announce a changed package set on, so the others rescan at once
        /// instead of waiting for their next periodic scan.
        /// </summary>
        internal const string ClusterTopic = "webexpress.packages";

        /// <summary>
        /// The name of the cluster lock that serializes every change of the shared catalog.
        /// </summary>
        internal const string ClusterLock = "packages";

        /// <summary>
        /// The directory below the package directory uploads and backups are kept in while an
        /// operation runs. Its content never ends in .wxp at the top of the package directory,
        /// so no scan - on this or another instance - takes it for a package.
        /// </summary>
        internal const string StagingDirectory = ".staging";

        // long enough for the slowest install, short enough that a crashed instance does not
        // block package changes for long
        private static readonly TimeSpan ClusterLockLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ClusterLockTimeout = TimeSpan.FromSeconds(30);

        // a staging directory this old belongs to no running operation any more
        private static readonly TimeSpan StagingLifetime = TimeSpan.FromHours(1);

        private IDisposable _clusterSubscription;
        private string _extractionRoot;

        // package files the scan refused, with the write time they were refused at, so that the
        // refusal is logged once per version of the file rather than every round
        private readonly Dictionary<string, DateTime> _refusedFiles = new(StringComparer.OrdinalIgnoreCase);

        // the entries of the plugins shipping with the application, by plugin id. they are kept
        // rather than built on every read because a deactivated or waiting one has no registered
        // plugin left to build it from
        private readonly Dictionary<string, PackageCatalogItem> _builtIns = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="pluginManager">The plugin manager.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private PackageManager(IComponentHub componentHub, IPluginManager pluginManager, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub as ComponentHub;
            _pluginManager = pluginManager as PluginManager;

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:packagemanager.initialization")
            );
        }

        /// <summary>
        /// Starts the manager.
        /// </summary>
        internal void Execute()
        {
            // the cluster lock keeps instances that start together from rewriting the catalog
            // while another reads it; an instance that does not get it in time still starts
            using var startup = Cluster?.Lock(ClusterLock, ClusterLockLifetime, ClusterLockTimeout);

            LoadCatalog();

            // an uninstalled built-in plugin is deleted before anything loads it - only now,
            // before the first load, is its assembly not locked
            DeleteRemovedBuiltIns();

            // load the default plugins
            _pluginManager.Register(Catalog.RemovedPlugins);

            // a built-in plugin an administrator deactivated is withdrawn before it boots, so it
            // never runs - not even for the moment until the catalog is read
            WithdrawDisabledBuiltIns(Catalog.DisabledPlugins);

            // boot default elements
            _componentHub?.BootComponent(_pluginManager.Plugins);

            RemoveStaleStaging();

            foreach (var package in Catalog.Packages)
            {
                var packagesFromFile = LoadPackage(Path.Combine(_httpServerContext?.PackagePath, package.File));

                package.Metadata = packagesFromFile?.Metadata;

                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate("webexpress.webcore:packagemanager.existing", package.File)
                );

                // nothing of the package is loaded yet, so whatever an earlier run could not
                // remove - an extraction whose assemblies were still locked - goes now
                RemoveExtractedDirectory(package);

                if (package.State != PackageCatalogItemState.Disable)
                {
                    package.State = PackageCatalogItemState.Available;
                }
            }

            LoadPending();
            SaveCatalog();
            startup?.Dispose();

            // build sitemap
            _componentHub?.SitemapManager.Refresh();

            _clusterSubscription = Cluster?.Subscribe(ClusterTopic, _ => _httpServerContext.Lifetime.TryRun(() => Scan()));

            _httpServerContext.Lifetime.TryRun(async stopping =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, TokenSource.Token);
                while (!linked.IsCancellationRequested)
                {
                    try
                    {
                        Scan();
                    }
                    catch (Exception ex)
                    {
                        // a failing round must not end the scanning for the rest of the process
                        _httpServerContext?.Log?.Exception(ex);
                    }

                    try
                    {
                        var secondsLeft = 60 - DateTime.Now.Second;
                        await Task.Delay(TimeSpan.FromSeconds(secondsLeft), linked.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // ShutDown cancels the manager's own token, which the lifetime does not
                        // recognize as a shutdown and would log as a failure
                        break;
                    }
                }
            });
        }

        /// <summary>
        /// Stop running the manager.
        /// </summary>
        public void ShutDown()
        {
            TokenSource.Cancel();
        }

        /// <summary>
        /// Searches the package directory for new, changed or removed packages.
        /// </summary>
        public void Scan()
        {
            lock (_scanLock)
            {
                var cluster = Cluster;

                // an instance changing the packages right now announces it when done; this round
                // is skipped rather than reading a catalog that is about to change
                using var coordination = cluster?.Lock(ClusterLock, ClusterLockLifetime, TimeSpan.Zero);

                if (cluster is not null && coordination is null)
                {
                    return;
                }

                ScanCore();
            }
        }

        /// <summary>
        /// Reconciles the complete package set while the local and cluster locks are held.
        /// Operations must see new and removed packages before saving the shared catalog.
        /// </summary>
        /// <param name="sharedOnly">True to discover only packages already recorded by another instance.</param>
        private void ScanCore(bool sharedOnly = false)
        {
            var packagePath = _httpServerContext?.PackagePath;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate
                (
                    "webexpress.webcore:packagemanager.scan",
                    packagePath
                )
            );

            // a missing directory - a share that is not mounted for a moment - says nothing
            // about the packages; reading it as empty would unload every one of them
            if (!Directory.Exists(packagePath))
            {
                return;
            }

            var shared = Cluster is not null ? ReadSharedStates() : null;
            var changed = AdoptSharedStates(shared);

            // determine all WebExpress packages from the file system
            var packageFiles = Directory.GetFiles(packagePath, "*.wxp").Select(x => Path.GetFileName(x)).ToList();
            var known = Catalog.Packages.Where(x => x is not null).ToList();

            // all packages that are not yet installed
            var newPackages = packageFiles.Except(known.Select(x => x.File), StringComparer.OrdinalIgnoreCase)
                .Where(file => !sharedOnly || shared?.Packages.ContainsKey(file) == true)
                .ToList();

            // all packages that are no longer available
            var removePackages = known.Where(x => !packageFiles.Contains(x.File, StringComparer.OrdinalIgnoreCase)).ToList();

            foreach (var file in _refusedFiles.Keys.Except(packageFiles, StringComparer.OrdinalIgnoreCase).ToList())
            {
                _refusedFiles.Remove(file);
            }

            // determine changed packages by comparing spec version and relevant metadata
            foreach (var existing in known.Except(removePackages))
            {
                var fromFile = LoadPackage(Path.Combine(packagePath, existing.File));
                if (!HasPackageChanged(existing, fromFile))
                {
                    continue;
                }

                _httpServerContext?.Log?.Info(I18N.Translate("webexpress.webcore:packagemanager.changed", existing.File));

                // respect disabled state; only update metadata without activating
                if (existing.State != PackageCatalogItemState.Disable)
                {
                    UnloadDependents(existing);
                    Unload(existing);

                    existing.State = PackageCatalogItemState.Available;
                }

                existing.Id = fromFile.Id;
                existing.Metadata = fromFile.Metadata;
                existing.Faulted = false;
                existing.ProblemText = null;
                changed = true;
            }

            foreach (var file in newPackages)
            {
                var fullPath = Path.Combine(packagePath, file);
                var writeTime = File.GetLastWriteTimeUtc(fullPath);

                if (_refusedFiles.TryGetValue(file, out var refusedAt) && refusedAt == writeTime)
                {
                    continue;
                }

                var packagesFromFile = LoadPackage(fullPath);
                if (packagesFromFile is null)
                {
                    _refusedFiles[file] = writeTime;

                    continue;
                }

                // a second file with the id of an installed package is a copy, not a package of
                // its own; taking it in would register the same plugins a second time
                if (Catalog.Packages.Any(x => x is not null && string.Equals(x.Id, packagesFromFile.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:packagemanager.duplicate", file, packagesFromFile.Id));
                    _refusedFiles[file] = writeTime;

                    continue;
                }

                // a package another instance installed disabled stays disabled here too
                packagesFromFile.State = shared is not null && shared.Packages.TryGetValue(file, out var state) && state == PackageCatalogItemState.Disable
                    ? PackageCatalogItemState.Disable
                    : PackageCatalogItemState.Available;

                if (shared is not null && shared.InstalledSettings.TryGetValue(file, out var installedSettings))
                {
                    packagesFromFile.InstalledSettings = [.. installedSettings];
                }

                Catalog.Packages.Add(packagesFromFile);

                // raise event for added package
                OnAddPackage(packagesFromFile);

                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:packagemanager.add",
                        file
                    )
                );

                changed = true;
            }

            foreach (var existing in removePackages)
            {
                // the file is gone and cannot be asked for; what depends on it waits for it
                UnloadDependents(existing);
                Unload(existing);

                // raise event before removing from catalog
                OnRemovePackage(existing);

                // remove package from catalog
                Catalog.Packages.Remove(existing);

                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:packagemanager.remove",
                        existing.File
                    )
                );

                changed = true;
            }

            if (LoadPending() || changed)
            {
                // build sitemap
                _componentHub?.SitemapManager.Refresh();

                // save the catalog
                SaveCatalog();
            }
        }

        /// <summary>
        /// Returns all package entries, the installed ones and the plugins that ship with the
        /// application.
        /// </summary>
        /// <remarks>
        /// The catalog only ever knows what was installed from a *.wxp file. In a plain build
        /// deployment every plugin is referenced statically, the catalog is empty, and a management
        /// surface reading it alone shows nothing while the server logs four running plugins. The
        /// union is formed here rather than in the pages so that a third consumer cannot inherit
        /// the blind spot; it is a read-only view and never touches
        /// <see cref="PackageCatalog.Packages"/>, which is what keeps the synthesized entries out
        /// of the persisted catalog.
        /// </remarks>
        /// <returns>An enumerable collection with all package entries.</returns>
        public IEnumerable<PackageCatalogItem> GetPackages()
        {
            lock (_scanLock)
            {
                var packages = Catalog.Packages.Where(x => x is not null).ToList();

                packages.AddRange(GetBuiltInPackages(packages));

                return packages;
            }
        }

        /// <summary>
        /// Returns a package by id, the installed ones as well as the built-in plugins.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The package or null.</returns>
        public PackageCatalogItem GetPackage(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return null;
            }

            lock (_scanLock)
            {
                return GetPackages()
                    .FirstOrDefault(x => x.Id is not null && x.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Returns the active packages that depend on a package, directly or through one of
        /// their plugins.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The dependent packages, empty when there are none or the package is unknown.</returns>
        public IEnumerable<PackageCatalogItem> GetDependentPackages(string packageId)
        {
            lock (_scanLock)
            {
                var package = GetPackage(packageId);

                return package is not null ? GetDependents(package) : [];
            }
        }

        /// <summary>
        /// Returns the entries of the plugins shipping with the application that no catalog entry
        /// accounts for.
        /// </summary>
        /// <remarks>
        /// A plugin is covered when a catalog entry lists it among its own plugins, or when a
        /// catalog entry carries its id - so a plugin that is present statically and as a package
        /// is reported once, by the package, which is the entry the operations can act on. A
        /// registered plugin nobody accounts for gets an entry of its own; an entry outlives its
        /// plugin only while it is deactivated or waiting, since that is when it is the one place
        /// that still knows the plugin.
        /// </remarks>
        /// <param name="packages">The installed packages the plugins are matched against.</param>
        /// <returns>The entries, ordered by id.</returns>
        private IEnumerable<PackageCatalogItem> GetBuiltInPackages(IEnumerable<PackageCatalogItem> packages)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in packages)
            {
                if (!string.IsNullOrWhiteSpace(package.Id))
                {
                    covered.Add(package.Id);
                }

                foreach (var plugin in package.Plugins.Where(x => x?.PluginId is not null))
                {
                    covered.Add(plugin.PluginId.ToString());
                }
            }

            var registered = (_pluginManager?.Plugins ?? [])
                .Where(x => x?.PluginId is not null)
                .ToList();

            // a running entry whose plugin was removed behind the manager's back is stale
            foreach (var stale in _builtIns.Values
                .Where(x => x.State == PackageCatalogItemState.Active && !x.Plugins.Any(registered.Contains))
                .ToList())
            {
                _builtIns.Remove(stale.Id);
            }

            foreach (var plugin in registered.Where(x => !covered.Contains(x.PluginId.ToString())))
            {
                if (!_builtIns.Values.Any(x => x.Plugins.Contains(plugin)) && !_builtIns.ContainsKey(plugin.PluginId.ToString()))
                {
                    var item = CreateBuiltInItem(plugin);

                    _builtIns[item.Id] = item;
                }
            }

            return _builtIns.Values
                .Where(x => !covered.Contains(x.Id))
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Creates the catalog entry that stands for a plugin loaded from the application directory.
        /// </summary>
        /// <param name="plugin">The plugin context.</param>
        /// <returns>The catalog entry.</returns>
        private PackageCatalogItem CreateBuiltInItem(IPluginContext plugin)
        {
            var id = plugin.PluginId.ToString();

            return new PackageCatalogItem()
            {
                Id = id,
                // there is no package file behind a built-in plugin. update and uninstall refuse
                // such an entry up front; the paths derived from the empty name are refused as
                // well, since an empty name would resolve to the package directory itself
                File = string.Empty,
                State = PackageCatalogItemState.Active,
                BuiltIn = true,
                SystemPlugin = plugin.Assembly?.GetCustomAttribute<SystemPluginAttribute>() is not null,
                Assembly = plugin.Assembly,
                Plugins = [plugin],
                Metadata = new PackageItem()
                {
                    FileName = string.Empty,
                    Id = id,
                    Version = plugin.Version,
                    Title = plugin.PluginName,
                    Authors = plugin.Manufacturer,
                    License = plugin.License,
                    Icon = plugin.Icon?.Display,
                    Description = plugin.Description,
                    PluginSources = [],
                    // the plugin's own dependencies, so a built-in plugin waits for and holds
                    // what it runs on exactly like a package does
                    Dependencies = [.. _pluginManager?.GetDependencies(plugin) ?? []]
                }
            };
        }

        /// <summary>
        /// Withdraws the built-in plugins an administrator deactivated, together with what runs
        /// on them, before anything of them boots.
        /// </summary>
        /// <param name="pluginIds">The ids of the deactivated plugins.</param>
        private void WithdrawDisabledBuiltIns(IEnumerable<string> pluginIds)
        {
            GetBuiltInPackages(Catalog.Packages.Where(x => x is not null));

            foreach (var id in pluginIds.ToList())
            {
                if (!_builtIns.TryGetValue(id, out var package) || package.SystemPlugin)
                {
                    continue;
                }

                UnloadDependents(package);
                Unload(package);
                package.State = PackageCatalogItemState.Disable;
            }
        }

        /// <summary>
        /// Deletes the assemblies of the built-in plugins an administrator uninstalled. An entry
        /// is dropped once its file is gone; one whose file the host's dependency manifest lists,
        /// or that cannot be deleted, stays and keeps the file from being loaded.
        /// </summary>
        private void DeleteRemovedBuiltIns()
        {
            foreach (var file in Catalog.RemovedPlugins.ToList())
            {
                var path = GetApplicationAssemblyPath(file);

                if (path is null || !File.Exists(path))
                {
                    Catalog.RemovedPlugins.Remove(file);

                    continue;
                }

                if (IsHostDependency(file))
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                    Catalog.RemovedPlugins.Remove(file);

                    _httpServerContext?.Log?.Info(Translate("builtin.deleted", file));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // a read-only application directory; the plugin is still not loaded
                    _httpServerContext?.Log?.Warning(Translate("builtin.kept", file, ex.Message));
                }
            }
        }

        /// <summary>
        /// Returns the path of an assembly file in the application directory.
        /// </summary>
        /// <param name="file">The file name.</param>
        /// <returns>The path, or null when the name does not denote a dll directly in the application directory.</returns>
        private static string GetApplicationAssemblyPath(string file)
        {
            if (string.IsNullOrWhiteSpace(file)
                || !string.Equals(Path.GetFileName(file), file, StringComparison.Ordinal)
                || !file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.Combine(AppContext.BaseDirectory, file);
        }

        /// <summary>
        /// Checks whether the host's dependency manifest lists an assembly file. The host refuses
        /// to start when such a file is missing, so it must not be deleted.
        /// </summary>
        /// <param name="file">The file name.</param>
        /// <returns>True when a manifest of the host lists the file.</returns>
        private static bool IsHostDependency(string file)
        {
            // the host joins the manifests with ';' on every platform
            var manifests = (AppContext.GetData("APP_CONTEXT_DEPS_FILES") as string)?
                .Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [];

            foreach (var manifest in manifests)
            {
                try
                {
                    var content = File.ReadAllText(manifest);

                    // entries name the file bare or below a package folder
                    if (content.Contains($"\"{file}\"", StringComparison.OrdinalIgnoreCase)
                        || content.Contains($"/{file}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // an unreadable manifest is treated as listing the file; keeping a file is
                    // recoverable, a host that does not start is not
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Rejects an operation that was asked to replace or remove a plugin shipping with the
        /// application.
        /// </summary>
        /// <remarks>
        /// Its assembly lives in the application directory, is loaded into the default context
        /// and cannot be replaced or removed while the process runs. Failing up front is what
        /// keeps a request from leaving the plugin half unregistered.
        /// </remarks>
        /// <param name="package">The package the operation was addressed to.</param>
        /// <returns>The failure result, or null when the package may be operated on.</returns>
        private static PackageOperationResult RejectBuiltIn(PackageCatalogItem package)
        {
            return package is not null && package.BuiltIn
                ? PackageOperationResult.Failed(Text("operation.builtin", package.Id), package)
                : null;
        }

        /// <summary>
        /// Rejects switching a system plugin on or off.
        /// </summary>
        /// <param name="package">The package the operation was addressed to.</param>
        /// <returns>The failure result, or null when the package may be switched.</returns>
        private static PackageOperationResult RejectSystemPlugin(PackageCatalogItem package)
        {
            return package is not null && package.SystemPlugin
                ? PackageOperationResult.Failed(Text("operation.system", package.Id), package)
                : null;
        }

        /// <summary>
        /// Validates a package file.
        /// </summary>
        /// <param name="packageFile">The package file path.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The validation result.</returns>
        public PackageValidationResult ValidatePackage(string packageFile, long maxPackageBytes = 0, string expectedSha256 = null)
        {
            var result = new PackageValidationResult();

            if (string.IsNullOrWhiteSpace(packageFile))
            {
                result.Add(Text("validation.path"));
                return result;
            }

            if (!File.Exists(packageFile))
            {
                result.Add(Text("validation.missing"));
                return result;
            }

            if (!Path.GetExtension(packageFile).Equals(".wxp", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(Text("validation.extension"));
                return result;
            }

            if (maxPackageBytes > 0)
            {
                var fileInfo = new FileInfo(packageFile);
                if (fileInfo.Length > maxPackageBytes)
                {
                    result.Add(Text("validation.size", maxPackageBytes));
                    return result;
                }
            }

            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                var hash = ComputeSha256(packageFile);
                if (!hash.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(Text("validation.hash"));
                    return result;
                }
            }

            try
            {
                using var zip = ZipFile.OpenRead(packageFile);
                var specEntry = zip.Entries.FirstOrDefault(x => Path.GetExtension(x.FullName).Equals(".spec", StringComparison.OrdinalIgnoreCase));
                if (specEntry is null)
                {
                    result.Add(Text("validation.nospec"));
                    return result;
                }

                var spec = ReadSpec(specEntry);
                if (spec is null)
                {
                    result.Add(Text("validation.spec"));
                    return result;
                }

                if (string.IsNullOrWhiteSpace(spec.Id))
                {
                    result.Add(Text("validation.id"));
                }

                if (string.IsNullOrWhiteSpace(spec.Version))
                {
                    result.Add(Text("validation.version"));
                }

                foreach (var plugin in spec.Plugins ?? [])
                {
                    if (string.IsNullOrWhiteSpace(plugin))
                    {
                        result.Add(Text("validation.plugin.empty"));
                        continue;
                    }

                    if (Path.IsPathRooted(plugin))
                    {
                        result.Add(Text("validation.plugin.rooted", plugin));
                    }

                    var normalized = plugin.Replace('\\', '/');
                    if (normalized.Contains("..", StringComparison.Ordinal))
                    {
                        result.Add(Text("validation.plugin.traversal", plugin));
                    }
                }

                result.Package = CreateCatalogItem(packageFile, spec, zip);
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
                result.Add(Text("validation.corrupt"));
            }

            result.IsValid = result.Messages.Count == 0;
            return result;
        }

        /// <summary>
        /// Uploads and installs a package from a stream.
        /// </summary>
        /// <param name="packageStream">The package stream.</param>
        /// <param name="fileName">The package file name.</param>
        /// <param name="activate">True to activate directly after install; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult UploadPackage(Stream packageStream, string fileName, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null)
        {
            return Upload(packageStream, fileName, null, activate, maxPackageBytes, expectedSha256);
        }

        /// <summary>
        /// Installs a package from a file.
        /// </summary>
        /// <param name="packageFile">The package file path.</param>
        /// <param name="activate">True to activate directly after install; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult InstallPackage(string packageFile, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null)
        {
            return Coordinate(() => InstallPackageCore(packageFile, activate, maxPackageBytes, expectedSha256, null));
        }

        /// <summary>
        /// Writes an upload to a staging directory of its own and installs it from there.
        /// </summary>
        /// <remarks>
        /// The upload reaches the package directory only after it passed every check, under the
        /// coordination of the catalog: a refused upload never replaces a package of the same
        /// name, and no scan sees a file that is still being written or checked.
        /// </remarks>
        /// <param name="packageStream">The package stream.</param>
        /// <param name="fileName">The package file name.</param>
        /// <param name="expectedId">The id the package must carry, or null for a new installation.</param>
        /// <param name="activate">True to activate directly after install; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult Upload(Stream packageStream, string fileName, string expectedId, bool activate, long maxPackageBytes, string expectedSha256)
        {
            if (packageStream is null || string.IsNullOrWhiteSpace(fileName))
            {
                return PackageOperationResult.Failed(Text("operation.upload"));
            }

            var safeFileName = Path.GetFileName(fileName);
            if (!safeFileName.EndsWith(".wxp", StringComparison.OrdinalIgnoreCase))
            {
                return PackageOperationResult.Failed(Text("validation.extension"));
            }

            string staging = null;

            try
            {
                staging = CreateStagingDirectory();
                var stagedFile = Path.Combine(staging, safeFileName);

                using (var fileStream = new FileStream(stagedFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (!CopyStream(packageStream, fileStream, maxPackageBytes))
                    {
                        return PackageOperationResult.Failed(Text("validation.size", maxPackageBytes));
                    }
                }

                return Coordinate(() => InstallPackageCore(stagedFile, activate, maxPackageBytes, expectedSha256, expectedId));
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
                return PackageOperationResult.Failed(Text("operation.upload"));
            }
            finally
            {
                if (staging is not null)
                {
                    RemoveStagingDirectory(staging);
                }
            }
        }

        /// <summary>
        /// Installs or updates a package from a file, with the catalog already coordinated.
        /// </summary>
        /// <param name="packageFile">The package file path.</param>
        /// <param name="activate">True to activate directly after install; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <param name="expectedId">The id of the package to update, or null to install or update whatever the file carries.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult InstallPackageCore(string packageFile, bool activate, long maxPackageBytes, string expectedSha256, string expectedId)
        {
            var validation = ValidatePackage(packageFile, maxPackageBytes, expectedSha256);
            if (!validation.IsValid)
            {
                return PackageOperationResult.Failed(PackageText.Join(validation.Texts));
            }

            var package = validation.Package;

            if (expectedId is not null && !package.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
            {
                return PackageOperationResult.Failed(Text("operation.mismatch", package.Id, expectedId));
            }

            var existing = Catalog.Packages
                .FirstOrDefault(x => x is not null && string.Equals(x.Id, package.Id, StringComparison.OrdinalIgnoreCase));

            if (expectedId is not null && existing is null)
            {
                return PackageOperationResult.Failed(Text("operation.notfound", expectedId));
            }

            package.File = Path.GetFileName(packageFile);

            // the file of another package would be replaced under its feet
            var owner = Catalog.Packages
                .FirstOrDefault(x => x is not null && x != existing && string.Equals(x.File, package.File, StringComparison.OrdinalIgnoreCase));

            if (owner is not null)
            {
                return PackageOperationResult.Failed(Text("operation.filetaken", package.File, owner.Id));
            }

            Directory.CreateDirectory(_httpServerContext?.PackagePath);

            return existing is null
                ? AddPackageCore(package, packageFile, activate)
                : ReplacePackageCore(existing, package, packageFile, activate);
        }

        /// <summary>
        /// Adds a package that is not installed yet.
        /// </summary>
        /// <param name="package">The validated package.</param>
        /// <param name="sourceFile">The file the package is installed from.</param>
        /// <param name="activate">True to activate the package.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult AddPackageCore(PackageCatalogItem package, string sourceFile, bool activate)
        {
            var targetFile = Path.Combine(_httpServerContext?.PackagePath, package.File);

            PlaceFile(sourceFile, targetFile);

            package.State = activate ? PackageCatalogItemState.Available : PackageCatalogItemState.Disable;
            Catalog.Packages.Add(package);
            OnAddPackage(package);

            var missing = GetUnfulfilledPackageDependencies(package).ToList();

            if (activate && missing.Count == 0 && !TryLoad(package, out var error))
            {
                // a package that does not start is not left behind half installed
                Catalog.Packages.Remove(package);
                OnRemovePackage(package);
                DeleteFile(targetFile);
                SaveCatalog();

                return PackageOperationResult.Failed(Text("operation.failed", package.Id, error));
            }

            // packages that waited for this one, or this one waiting for its dependencies
            LoadPending();
            SaveCatalog();
            _componentHub?.SitemapManager.Refresh();

            return package.State switch
            {
                PackageCatalogItemState.Active => PackageOperationResult.Ok(Text("operation.installed", package.Id), package),
                PackageCatalogItemState.Disable => PackageOperationResult.Ok(Text("operation.installed.disabled", package.Id), package),
                _ => PackageOperationResult.Ok(Text("operation.installed.waiting", package.Id, string.Join(", ", missing)), package)
            };
        }

        /// <summary>
        /// Replaces an installed package with a new version and rolls back to the previous one
        /// when the new version does not start.
        /// </summary>
        /// <param name="existing">The installed package.</param>
        /// <param name="package">The validated new version.</param>
        /// <param name="sourceFile">The file the new version is installed from.</param>
        /// <param name="activate">True to activate the new version.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult ReplacePackageCore(PackageCatalogItem existing, PackageCatalogItem package, string sourceFile, bool activate)
        {
            var packagePath = _httpServerContext?.PackagePath;

            if (activate)
            {
                // checked before anything is touched: an update that cannot start must leave the
                // running version in place
                var missing = GetUnfulfilledPackageDependencies(package).ToList();
                if (missing.Count > 0)
                {
                    return PackageOperationResult.Failed(Text("operation.dependencies", package.Id, string.Join(", ", missing)), existing);
                }
            }

            var previousFile = existing.File;
            var previousMetadata = existing.Metadata;
            var previousState = existing.State;
            var oldFile = Path.Combine(packagePath, previousFile);
            var targetFile = Path.Combine(packagePath, package.File);
            var backup = CreateStagingDirectory();
            var backupFile = Path.Combine(backup, previousFile);

            try
            {
                if (File.Exists(oldFile))
                {
                    File.Copy(oldFile, backupFile);
                }

                UnloadDependents(existing);
                Unload(existing);

                PlaceFile(sourceFile, targetFile);

                if (!oldFile.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                {
                    DeleteFile(oldFile);
                }

                existing.File = package.File;
                existing.Metadata = package.Metadata;
                existing.Faulted = false;
                existing.ProblemText = null;
                existing.State = activate ? PackageCatalogItemState.Available : PackageCatalogItemState.Disable;

                if (activate && !TryLoad(existing, out var error))
                {
                    // the new version does not start; the previous one is put back as it was
                    if (!oldFile.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                    {
                        DeleteFile(targetFile);
                    }

                    if (File.Exists(backupFile))
                    {
                        PlaceFile(backupFile, oldFile);
                    }

                    existing.File = previousFile;
                    existing.Metadata = previousMetadata;
                    existing.Faulted = false;
                    existing.ProblemText = null;
                    existing.State = previousState == PackageCatalogItemState.Disable
                        ? PackageCatalogItemState.Disable
                        : PackageCatalogItemState.Available;

                    LoadPending();
                    SaveCatalog();
                    _componentHub?.SitemapManager.Refresh();

                    return PackageOperationResult.Failed(Text("operation.update.failed", existing.Id, error), existing);
                }

                // the dependents unloaded above start again on the new version
                LoadPending();
                SaveCatalog();
                _componentHub?.SitemapManager.Refresh();

                return PackageOperationResult.Ok(Text("operation.updated", existing.Id), existing);
            }
            finally
            {
                RemoveStagingDirectory(backup);
            }
        }

        /// <summary>
        /// Activates a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult ActivatePackage(string packageId)
        {
            return Coordinate(() => ActivatePackageCore(packageId));
        }

        /// <summary>
        /// Activates a package, with the catalog already coordinated.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult ActivatePackageCore(string packageId)
        {
            var package = GetPackage(packageId);
            if (package is null)
            {
                return PackageOperationResult.Failed(Text("operation.notfound", packageId));
            }

            var system = RejectSystemPlugin(package);
            if (system is not null)
            {
                return system;
            }

            if (package.State == PackageCatalogItemState.Active)
            {
                return PackageOperationResult.Ok(Text("operation.active", packageId), package);
            }

            var missingDependencies = GetUnfulfilledPackageDependencies(package).ToList();
            if (missingDependencies.Count > 0)
            {
                return PackageOperationResult.Failed(Text("operation.dependencies", packageId, string.Join(", ", missingDependencies)), package);
            }

            var previousState = package.State;

            if (!TryLoad(package, out var error))
            {
                // a disabled package stays disabled; the administrator's decision did not change
                if (previousState == PackageCatalogItemState.Disable)
                {
                    package.State = PackageCatalogItemState.Disable;
                }

                SaveCatalog();
                _componentHub?.SitemapManager.Refresh();

                return PackageOperationResult.Failed(Text("operation.failed", packageId, error), package);
            }

            LoadPending();
            SaveCatalog();
            _componentHub?.SitemapManager.Refresh();

            return PackageOperationResult.Ok(Text("operation.activated", packageId), package);
        }

        /// <summary>
        /// Deactivates a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult DeactivatePackage(string packageId)
        {
            return Coordinate(() => DeactivatePackageCore(packageId));
        }

        /// <summary>
        /// Deactivates a package, with the catalog already coordinated.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult DeactivatePackageCore(string packageId)
        {
            var package = GetPackage(packageId);
            if (package is null)
            {
                return PackageOperationResult.Failed(Text("operation.notfound", packageId));
            }

            var system = RejectSystemPlugin(package);
            if (system is not null)
            {
                return system;
            }

            if (package.State == PackageCatalogItemState.Disable)
            {
                return PackageOperationResult.Ok(Text("operation.inactive", packageId), package);
            }

            // what runs on the package is stopped with it rather than keeping it on: the
            // dependents are not disabled but wait, and start again once it is activated
            var stopped = UnloadDependents(package);

            Unload(package);
            package.State = PackageCatalogItemState.Disable;
            package.Faulted = false;
            package.ProblemText = null;

            SaveCatalog();
            _componentHub?.SitemapManager.Refresh();

            return stopped.Count > 0
                ? PackageOperationResult.Ok(Text("operation.deactivated.dependents", packageId, string.Join(", ", stopped.Select(x => x.Id))), package)
                : PackageOperationResult.Ok(Text("operation.deactivated", packageId), package);
        }

        /// <summary>
        /// Updates a package from a file path.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageFile">The package file path.</param>
        /// <param name="activate">True to activate directly after update; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult UpdatePackage(string packageId, string packageFile, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null)
        {
            return RejectBuiltIn(GetPackage(packageId))
                ?? Coordinate(() => InstallPackageCore(packageFile, activate, maxPackageBytes, expectedSha256, packageId));
        }

        /// <summary>
        /// Updates a package from an uploaded stream.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageStream">The package stream.</param>
        /// <param name="fileName">The package file name.</param>
        /// <param name="activate">True to activate directly after update; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult UpdatePackage(string packageId, Stream packageStream, string fileName, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null)
        {
            return RejectBuiltIn(GetPackage(packageId))
                ?? Upload(packageStream, fileName, packageId, activate, maxPackageBytes, expectedSha256);
        }

        /// <summary>
        /// Uninstalls and removes a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="removeSettings">True to also delete the settings files the package ships.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult UninstallPackage(string packageId, bool removeSettings = false)
        {
            return Coordinate(() => UninstallPackageCore(packageId, removeSettings));
        }

        /// <summary>
        /// Uninstalls and removes a package, with the catalog already coordinated.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="removeSettings">True to also delete the settings files the package ships.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult UninstallPackageCore(string packageId, bool removeSettings)
        {
            var package = GetPackage(packageId);
            if (package is null)
            {
                return PackageOperationResult.Failed(Text("operation.notfound", packageId));
            }

            var system = RejectSystemPlugin(package);
            if (system is not null)
            {
                return system;
            }

            var dependents = RejectDependents(package);
            if (dependents is not null)
            {
                return dependents;
            }

            if (package.BuiltIn)
            {
                return UninstallBuiltInCore(package, removeSettings);
            }

            Unload(package);
            DeleteFile(Path.Combine(_httpServerContext?.PackagePath, package.File));

            var settingsRemoved = removeSettings && RemoveSettings(package);

            Catalog.Packages.Remove(package);
            OnRemovePackage(package);

            SaveCatalog();

            if (settingsRemoved)
            {
                _httpServerContext?.Configuration?.Reload();
            }

            _componentHub?.SitemapManager.Refresh();

            return PackageOperationResult.Ok(Text("operation.uninstalled", packageId), package);
        }

        /// <summary>
        /// Uninstalls a plugin shipping with the application.
        /// </summary>
        /// <remarks>
        /// Its plugin is withdrawn at once. The assembly cannot go with it: it is loaded into the
        /// default context and, on windows, locked until the process ends. It is recorded in the
        /// catalog instead and deleted on the next start, before the plugins are loaded.
        /// </remarks>
        /// <param name="package">The entry of the plugin.</param>
        /// <param name="removeSettings">True to also delete the settings files the plugin ships.</param>
        /// <returns>The operation result.</returns>
        private PackageOperationResult UninstallBuiltInCore(PackageCatalogItem package, bool removeSettings)
        {
            var file = Path.GetFileName(package.Assembly?.Location ?? string.Empty);

            // an assembly loaded from memory has no file to remove on the next start
            if (GetApplicationAssemblyPath(file) is null)
            {
                return PackageOperationResult.Failed(Text("operation.builtin", package.Id), package);
            }

            Unload(package);
            _builtIns.Remove(package.Id);
            Catalog.DisabledPlugins.RemoveAll(x => string.Equals(x, package.Id, StringComparison.OrdinalIgnoreCase));

            if (!Catalog.RemovedPlugins.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                Catalog.RemovedPlugins.Add(file);
            }

            var settingsRemoved = removeSettings && RemoveSettings(package);

            OnRemovePackage(package);
            SaveCatalog();

            if (settingsRemoved)
            {
                _httpServerContext?.Configuration?.Reload();
            }

            _componentHub?.SitemapManager.Refresh();

            return IsHostDependency(file)
                ? PackageOperationResult.Ok(Text("operation.uninstalled.manifest", package.Id, file), package)
                : PackageOperationResult.Ok(Text("operation.uninstalled.builtin", package.Id, file), package);
        }

        /// <summary>
        /// Refuses to take away a package other active packages run on.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <returns>The failure result, or null when nothing depends on the package.</returns>
        private PackageOperationResult RejectDependents(PackageCatalogItem package)
        {
            var dependents = GetDependents(package);

            return dependents.Count > 0
                ? PackageOperationResult.Failed(Text("operation.dependents", package.Id, string.Join(", ", dependents.Select(x => x.Id))), package)
                : null;
        }

        /// <summary>
        /// Deletes the settings files a package ships from the settings directory of the server.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <returns>True when a file was deleted, so the configuration has to be reloaded.</returns>
        private bool RemoveSettings(PackageCatalogItem package)
        {
            var settingsPath = _httpServerContext?.SettingsPath;
            var removed = false;

            if (string.IsNullOrWhiteSpace(settingsPath))
            {
                return false;
            }

            // only files this package actually deployed may be removed
            foreach (var name in package.InstalledSettings)
            {
                // a damaged catalog must never turn an owned file name into an outside path
                if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name
                    || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("webexpress.settings.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var file = Path.Combine(settingsPath, name);

                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        removed = true;

                        _httpServerContext?.Log?.Info(Translate("settings.removed", name));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _httpServerContext?.Log?.Warning(Translate("settings.unwritable", name, ex.Message));
                }
            }

            return removed;
        }

        /// <summary>
        /// Opens a package and finds the meta information.
        /// </summary>
        /// <param name="file">The path and file name.</param>
        /// <returns>The package information as a catalog entry.</returns>
        private PackageCatalogItem LoadPackage(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    using var zip = ZipFile.Open(file, ZipArchiveMode.Read);

                    var specEntry = zip.Entries
                        .FirstOrDefault(x => Path.GetExtension(x.FullName).Equals(".spec", StringComparison.OrdinalIgnoreCase));
                    if (specEntry is null)
                    {
                        _httpServerContext?.Log?.Warning($"package spec was not found in '{file}'");
                        return null;
                    }

                    var spec = ReadSpec(specEntry);
                    return CreateCatalogItem(file, spec, zip);
                }
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate
                (
                    "webexpress.webcore:packagemanager.packagenotfound",
                    file
                )
            );

            return null;
        }

        /// <summary>
        /// Load the catalog.
        /// </summary>
        private void LoadCatalog()
        {
            var catalogeFile = Path.Combine(_httpServerContext?.PackagePath, "catalog.xml");
            if (File.Exists(catalogeFile))
            {
                // shared for deletion, so another instance can replace the catalog meanwhile -
                // windows refuses to rename over a file opened without it
                using var catalog = new StreamReader(new FileStream(catalogeFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));

                if (catalog.BaseStream.Length == 0)
                {
                    return;
                }

                var serializer = new XmlSerializer(typeof(PackageCatalog));
                var items = (PackageCatalog)serializer.Deserialize(catalog);

                Catalog.Packages.Clear();

                foreach (var item in items.Packages)
                {
                    // an entry without a plain package file name - a catalog edited by hand or
                    // written half - would derive its paths from the package directory itself
                    if (!IsPackageFileName(item?.File))
                    {
                        _httpServerContext?.Log?.Warning(Translate("catalog.invalid", item?.Id));

                        continue;
                    }

                    Catalog.Packages.Add(item);
                }

                Catalog.DisabledPlugins.Clear();
                Catalog.DisabledPlugins.AddRange(items.DisabledPlugins.Where(x => !string.IsNullOrWhiteSpace(x)));

                Catalog.RemovedPlugins.Clear();
                Catalog.RemovedPlugins.AddRange(items.RemovedPlugins.Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            Log();
        }

        /// <summary>
        /// Save the catalog.
        /// </summary>
        private void SaveCatalog()
        {
            var catalogeFile = Path.Combine(_httpServerContext?.PackagePath, "catalog.xml");
            var temporary = catalogeFile + "." + Guid.NewGuid().ToString("N") + ".tmp";

            // a built-in plugin missing from this instance - one only another instance of a
            // cluster ships - keeps the state that instance recorded for it
            var disabled = Catalog.DisabledPlugins
                .Where(x => !_builtIns.ContainsKey(x))
                .Concat(_builtIns.Values.Where(x => x.State == PackageCatalogItemState.Disable).Select(x => x.Id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Catalog.DisabledPlugins.Clear();
            Catalog.DisabledPlugins.AddRange(disabled);

            using (var fs = new FileStream(temporary, FileMode.CreateNew))
            using (var writer = new XmlTextWriter(fs, Encoding.Unicode))
            {
                var serializer = new XmlSerializer(typeof(PackageCatalog));

                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, Catalog, new XmlSerializerNamespaces([new XmlQualifiedName("", "")]));
            }

            try
            {
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        File.Move(temporary, catalogeFile, true);
                        break;
                    }
                    catch (Exception ex) when (attempt < 5 && ex is IOException or UnauthorizedAccessException)
                    {
                        // another instance replacing or reading the catalog holds it for a moment
                        Thread.Sleep(20 << attempt);
                    }
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:packagemanager.save")
            );
        }

        /// <summary>
        /// Starts a package: extracts, registers and boots it.
        /// </summary>
        /// <remarks>
        /// A failure takes out again whatever of the package got registered, so a failed start
        /// leaves nothing half running. The package is marked as faulted and is not retried by
        /// the scan until its file changes or an administrator activates it.
        /// </remarks>
        /// <param name="package">The package.</param>
        /// <param name="error">The reason the start failed, or null.</param>
        /// <returns>True when the package runs.</returns>
        private bool TryLoad(PackageCatalogItem package, out PackageText error)
        {
            try
            {
                ExtractPackage(package);
                RegisterPackage(package);
                BootPackage(package);

                package.State = PackageCatalogItemState.Active;
                package.Faulted = false;
                package.ProblemText = null;
                error = null;

                return true;
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);

                Unload(package);

                package.Faulted = true;
                error = ex is PackageLoadException load ? load.Reason : PackageText.Literal(ex.Message);
                package.ProblemText = Text("problem.failed", error);

                return false;
            }
        }

        /// <summary>
        /// Starts every available package whose dependencies are active, repeating until a
        /// round starts nothing, so a package listed before its dependency starts in the round
        /// after it.
        /// </summary>
        /// <returns>True when a package was started.</returns>
        private bool LoadPending()
        {
            var loaded = false;
            bool progress;

            do
            {
                progress = false;

                // built-in plugins included: one stopped with what it runs on comes back with it
                foreach (var package in GetPackages()
                    .Where(x => x.State == PackageCatalogItemState.Available && !x.Faulted && x.Metadata is not null)
                    .ToList())
                {
                    var missing = GetUnfulfilledPackageDependencies(package).ToList();
                    if (missing.Count > 0)
                    {
                        package.ProblemText = Text("problem.dependencies", string.Join(", ", missing));

                        continue;
                    }

                    if (TryLoad(package, out _))
                    {
                        progress = true;
                        loaded = true;
                    }
                }
            } while (progress);

            return loaded;
        }

        /// <summary>
        /// Stops a package: shuts down and removes its plugins and removes its extraction.
        /// </summary>
        /// <param name="package">The package.</param>
        private void Unload(PackageCatalogItem package)
        {
            if (package is null)
            {
                return;
            }

            // one plugin failing to shut down must not keep the others of the package loaded
            foreach (var pluginContext in package.Plugins.ToList())
            {
                try
                {
                    _componentHub?.ShutDownComponent(pluginContext);
                }
                catch (Exception ex)
                {
                    _httpServerContext?.Log?.Exception(ex);
                }

                try
                {
                    _pluginManager.Remove(pluginContext);
                }
                catch (Exception ex)
                {
                    _httpServerContext?.Log?.Exception(ex);
                }
            }

            package.Plugins.Clear();
            package.State = PackageCatalogItemState.Available;

            // the name and description of a built-in plugin are keys into its own texts, which
            // left with the plugin; its assembly stays loaded, so the texts are kept for the entry
            // that still lists it
            if (package is { BuiltIn: true, Assembly: not null })
            {
                (_componentHub?.InternationalizationManager as InternationalizationManager)?.Register(package.Assembly, package.Id);
            }

            RemoveExtractedDirectory(package);
        }

        /// <summary>
        /// Unloads the active packages that depend on a package that goes away, the indirect
        /// dependents before the direct ones. They are not disabled: they wait as available
        /// packages and start again once what they need is back.
        /// </summary>
        /// <param name="package">The package that goes away.</param>
        /// <returns>The packages that were stopped.</returns>
        private List<PackageCatalogItem> UnloadDependents(PackageCatalogItem package)
        {
            var visited = new HashSet<PackageCatalogItem> { package };
            var order = new List<PackageCatalogItem>();

            void Visit(PackageCatalogItem current)
            {
                foreach (var dependent in GetDependents(current))
                {
                    if (visited.Add(dependent))
                    {
                        Visit(dependent);
                        order.Add(dependent);
                    }
                }
            }

            Visit(package);

            foreach (var dependent in order)
            {
                Unload(dependent);
                dependent.ProblemText = Text("problem.dependencies", package.Id);
            }

            return order;
        }

        /// <summary>
        /// Returns the active packages that depend on a package.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <returns>The dependent packages.</returns>
        private List<PackageCatalogItem> GetDependents(PackageCatalogItem package)
        {
            var identities = GetIdentities(package);

            // built-in plugins run on packages and on each other as well
            return [.. GetPackages().Where(x => x != package
                && x.State == PackageCatalogItemState.Active
                && DependsOn(x, identities))];
        }

        /// <summary>
        /// Returns the names a dependency can refer to a package by: its id and the ids of its
        /// plugins.
        /// </summary>
        /// <remarks>
        /// A plugin id is the namespace of the plugin, which by convention is the name of its
        /// assembly and thereby of its plugin source; the source names identify the plugins of
        /// a package that is not loaded and has no plugin contexts.
        /// </remarks>
        /// <param name="package">The package.</param>
        /// <returns>The identities, compared case-insensitively.</returns>
        private static HashSet<string> GetIdentities(PackageCatalogItem package)
        {
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var identity in new[] { package?.Id, package?.Metadata?.Id }
                .Concat(package?.Plugins.Select(x => x?.PluginId?.ToString()) ?? [])
                .Concat(package?.Metadata?.PluginSources?.Select(x => Path.GetFileName(x)) ?? []))
            {
                if (!string.IsNullOrWhiteSpace(identity))
                {
                    identities.Add(identity);
                }
            }

            return identities;
        }

        /// <summary>
        /// Checks whether a package depends on any of the given identities, through its own
        /// dependencies or through the dependencies of its plugins.
        /// </summary>
        /// <param name="candidate">The package that may depend on them.</param>
        /// <param name="identities">The identities of the package depended on.</param>
        /// <returns>True when it depends on them.</returns>
        private bool DependsOn(PackageCatalogItem candidate, HashSet<string> identities)
        {
            foreach (var dependency in candidate.Metadata?.Dependencies ?? [])
            {
                if (TryParseDependency(dependency, out var id, out _, out _) && identities.Contains(id))
                {
                    return true;
                }
            }

            return candidate.Plugins.Any(x => _pluginManager.GetDependencies(x).Any(identities.Contains));
        }

        /// <summary>
        /// Extracts the specified package to the file system.
        /// </summary>
        /// <param name="package">The package.</param>
        private void ExtractPackage(PackageCatalogItem package)
        {
            var packageFile = Path.Combine(_httpServerContext?.PackagePath, package?.File);

            if (!File.Exists(packageFile))
            {
                return;
            }

            var packageDirectory = GetExtractionDirectory(package)
                ?? throw new InvalidOperationException(Translate("catalog.invalid", package?.Id));

            // every start extracts into a fresh directory: on windows the assemblies of the
            // previous start stay locked until the runtime has collected their load context,
            // which happens some time after the unload, so extracting over them would fail
            var extractedPath = Path.Combine(packageDirectory, Guid.NewGuid().ToString("N")[..8]);
            var extractedPathFull = Path.GetFullPath(extractedPath);

            using var zip = ZipFile.Open(packageFile, ZipArchiveMode.Read);

            Directory.CreateDirectory(extractedPath);
            package.ExtractedPath = extractedPath;

            var scopes = GetIdentities(package)
                .Select(x => ConfigurationPath.Combine(HttpServerSettings.PluginSection, x))
                .ToList();
            var deployedSettings = false;

            foreach (var entry in zip.Entries)
            {
                var normalized = (entry.FullName ?? string.Empty).Replace('\\', '/').TrimStart('/');

                if (normalized.StartsWith(PackageBuilder.SettingsDirectory + "/", StringComparison.OrdinalIgnoreCase))
                {
                    if (DeploySettings(entry, normalized, scopes))
                    {
                        var name = GetSettingsFileName(entry, normalized);
                        if (!package.InstalledSettings.Contains(name, StringComparer.OrdinalIgnoreCase))
                        {
                            package.InstalledSettings.Add(name);
                        }
                        deployedSettings = true;
                    }

                    continue;
                }

                if (!normalized.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (normalized.Contains("../", StringComparison.Ordinal) || normalized.StartsWith("..", StringComparison.Ordinal))
                {
                    _httpServerContext?.Log?.Warning($"Unsafe package entry '{entry.FullName}' ignored.");
                    continue;
                }

                var targetFilePath = Path.GetFullPath(Path.Combine(extractedPath, normalized));
                var isInExtractedPath = targetFilePath.StartsWith(extractedPathFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || targetFilePath.Equals(extractedPathFull, StringComparison.OrdinalIgnoreCase);
                if (!isInExtractedPath)
                {
                    _httpServerContext?.Log?.Warning($"Unsafe package entry '{entry.FullName}' ignored.");
                    continue;
                }

                // directory entries in the zip have an empty Name
                if (string.IsNullOrEmpty(entry.Name))
                {
                    var dirPath = targetFilePath;
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }

                    continue;
                }

                var targetDir = Path.GetDirectoryName(targetFilePath);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                entry.ExtractToFile(targetFilePath, true);
            }

            // the configuration everyone holds is reloaded in place, so the plugin about to
            // boot finds its settings without a restart
            if (deployedSettings)
            {
                _httpServerContext?.Configuration?.Reload();
            }
        }

        /// <summary>
        /// Deploys a settings file of a package to the settings directory of the server. An
        /// existing file is left alone: it is the administrator's by then, and a package update
        /// must not undo the changes made to it.
        /// </summary>
        /// <remarks>
        /// Every file in the settings directory is merged into the one configuration the whole
        /// server reads, so a package could otherwise set server keys the main file leaves out
        /// (endpoints, security, authentication) or replace the settings of another plugin. The
        /// file is therefore only deployed when every key it sets lies below
        /// <c>Plugins:&lt;id&gt;</c> of the package itself or of one of its own plugins.
        /// </remarks>
        /// <param name="entry">The archive entry of the settings file.</param>
        /// <param name="normalized">The entry path with forward slashes and no leading slash.</param>
        /// <param name="scopes">The configuration sections the package may set keys in.</param>
        /// <returns><see langword="true"/> when the file was written, <see langword="false"/> when it was skipped.</returns>
        private bool DeploySettings(ZipArchiveEntry entry, string normalized, IReadOnlyCollection<string> scopes)
        {
            var settingsPath = _httpServerContext?.SettingsPath;
            var name = GetSettingsFileName(entry, normalized);

            // directory entries carry no file
            if (string.IsNullOrEmpty(entry.Name) || string.IsNullOrWhiteSpace(settingsPath))
            {
                return false;
            }

            // only json files directly below the settings directory are settings; a nested path
            // could escape the directory and another extension would never be merged
            if (name is null)
            {
                _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:packagemanager.settings.ignored", entry.FullName));

                return false;
            }

            var targetFilePath = Path.Combine(settingsPath, name);

            if (File.Exists(targetFilePath))
            {
                _httpServerContext?.Log?.Debug(I18N.Translate("webexpress.webcore:packagemanager.settings.existing", name));

                return false;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var content = buffer.ToArray();

            // a file that does not parse is refused before it reaches the directory: once there,
            // it would fail every following start of the server, not just this installation
            var keys = ReadSettingsKeys(content);

            if (keys is null)
            {
                _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:packagemanager.settings.invalid", entry.FullName));

                return false;
            }

            var foreignKey = keys.FirstOrDefault(x => !scopes.Any(scope => IsWithinScope(x, scope)));

            if (foreignKey is not null)
            {
                _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:packagemanager.settings.outofscope", entry.FullName, foreignKey, string.Join(", ", scopes)));

                return false;
            }

            try
            {
                Directory.CreateDirectory(settingsPath);

                // create-new, so of several instances deploying the same package only one writes
                using var target = new FileStream(targetFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                target.Write(content);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a settings directory mounted read-only (a config map) or an instance that wrote
                // the file first; either way the package boots with what the directory holds
                _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:packagemanager.settings.unwritable", name, ex.Message));

                return false;
            }

            _httpServerContext?.Log?.Info(I18N.Translate("webexpress.webcore:packagemanager.settings.deployed", name));

            return true;
        }

        /// <summary>
        /// Returns the name a settings entry of a package is deployed under.
        /// </summary>
        /// <param name="entry">The archive entry.</param>
        /// <param name="normalized">The entry path with forward slashes and no leading slash.</param>
        /// <returns>The file name, or null when the entry is no settings file.</returns>
        private static string GetSettingsFileName(ZipArchiveEntry entry, string normalized)
        {
            var segments = normalized.Split('/');

            return !string.IsNullOrEmpty(entry.Name)
                && segments.Length == 2
                && segments[0].Equals(PackageBuilder.SettingsDirectory, StringComparison.OrdinalIgnoreCase)
                && segments[1].EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && segments[1].IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                    ? segments[1]
                    : null;
        }

        /// <summary>
        /// Reads the configuration keys a settings file would set. The file is parsed by the same
        /// json configuration provider the settings directory uses, so comments, trailing commas
        /// and colons inside property names (<c>"Plugins:other": {}</c>) are flattened exactly as
        /// they would be once the file is deployed.
        /// </summary>
        /// <param name="content">The content of the settings file.</param>
        /// <returns>The keys that carry a value, or null when the configuration would refuse the content.</returns>
        private static IReadOnlyList<string> ReadSettingsKeys(byte[] content)
        {
            try
            {
                using var stream = new MemoryStream(content);
                var configuration = new ConfigurationBuilder()
                    .AddJsonStream(stream)
                    .Build();

                using (configuration as IDisposable)
                {
                    // the settings directory merges only keys with a value; containers set nothing
                    return [.. configuration.AsEnumerable().Where(x => x.Value is not null).Select(x => x.Key)];
                }
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// Checks whether a configuration key lies at or below the given section.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="scope">The section the key must stay within.</param>
        /// <returns><see langword="true"/> when the key belongs to the section.</returns>
        private static bool IsWithinScope(string key, string scope)
        {
            // configuration keys are case-insensitive, so a differently cased key reaches the same section
            return key.Equals(scope, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(scope + ConfigurationPath.KeyDelimiter, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers the plungins included in the package.
        /// </summary>
        /// <param name="package">The package.</param>
        private void RegisterPackage(PackageCatalogItem package)
        {
            // a built-in plugin is not extracted; its assembly never left the default context
            if (package is { BuiltIn: true, Assembly: not null })
            {
                var contexts = _pluginManager.Register(package.Assembly).ToList();

                if (contexts.Count == 0)
                {
                    throw new PackageLoadException(Text("plugin.notregistered", package.Id));
                }

                package.Plugins.AddRange(contexts);

                return;
            }

            if (package?.ExtractedPath is null)
            {
                return;
            }

            // load plugins
            foreach (var plugin in package.Metadata?.PluginSources ?? [])
            {
                var pluginFile = GetTargetPath(package, plugin);
                var pluginContexts = _pluginManager.Register(pluginFile).ToList();

                // a plugin file that yields no plugin holds one whose id is taken - by a built-in
                // plugin or another package - or none at all; the package would run in name only
                if (pluginContexts.Count == 0 && File.Exists(pluginFile))
                {
                    throw new PackageLoadException(Text("plugin.notregistered", plugin));
                }

                package.Plugins.AddRange(pluginContexts);
            }
        }

        /// <summary>
        /// Boots the components included in the package.
        /// </summary>
        /// <param name="package">The package.</param>
        private void BootPackage(PackageCatalogItem package)
        {
            _componentHub?.BootComponent(package.Plugins);
        }

        /// <summary>
        /// Determines the target directory where the plug-ins of the package are located
        /// for the current target platform.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="plugin">The plugin.</param>
        /// <returns>The directory (absolutely).</returns>
        private static string GetTargetPath(PackageCatalogItem package, string plugin)
        {
            return Path.GetFullPath(Path.Combine
            (
                package.ExtractedPath, plugin, GetTFM(), $"{Path.GetFileName(plugin)}.dll"
            ));
        }

        /// <summary>
        /// Determines the target framework.
        /// </summary>
        /// <returns>The TFM</returns>
        private static string GetTFM()
        {
            var targetFrameworkAttribute = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
                    .Select(x => x as TargetFrameworkAttribute)
                    .SingleOrDefault();

            return targetFrameworkAttribute.FrameworkDisplayName.Replace(" ", "").ToLower().Replace(".net", "net");
        }

        /// <summary>
        /// Raises the AddPackage event.
        /// </summary>
        /// <param name="item">The package catalog item.</param>
        private void OnAddPackage(PackageCatalogItem item)
        {
            AddPackage?.Invoke(this, item);
        }

        /// <summary>
        /// Raises the RemovePackage event.
        /// </summary>
        /// <param name="item">The package catalog item.</param>
        private void OnRemovePackage(PackageCatalogItem item)
        {
            RemovePackage?.Invoke(this, item);
        }

        /// <summary>
        /// Information about the component is collected and prepared for output in the log.
        /// </summary>
        private void Log()
        {
            if (Catalog.Packages.Count == 0)
            {
                return;
            }

            using var frame = new LogFrameSimple(_httpServerContext?.Log);
            var list = new List<string>
            {
                I18N.Translate("webexpress.webcore:packagemanager.titel")
            };

            foreach (var package in Catalog.Packages)
            {
                list.Add
                (
                    I18N.Translate("webexpress.webcore:packagemanager.package", package.Id)
                );
            }

            _httpServerContext?.Log?.Info(string.Join(Environment.NewLine, list));
        }

        /// <summary>
        /// Checks if a package has changed by comparing spec-relevant metadata.
        /// </summary>
        /// <param name="existing">The existing catalog item.</param>
        /// <param name="fromFile">The catalog item loaded from file.</param>
        /// <returns>True if changed; otherwise false.</returns>
        private static bool HasPackageChanged(PackageCatalogItem existing, PackageCatalogItem fromFile)
        {
            if (existing is null || fromFile?.Metadata is null)
            {
                return false;
            }

            // a package whose file could not be read at startup has no metadata; a readable file
            // now is the change it waited for
            if (existing.Metadata is null)
            {
                return true;
            }

            if (!string.Equals(existing.Metadata.Version, fromFile.Metadata.Version, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // compare plugin sources sequence-insensitively
            var a = (existing.Metadata.PluginSources ?? []).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var b = (fromFile.Metadata.PluginSources ?? []).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

            if (a.Length != b.Length)
            {
                return true;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var dependenciesA = (existing.Metadata.Dependencies ?? []).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var dependenciesB = (fromFile.Metadata.Dependencies ?? []).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

            if (dependenciesA.Length != dependenciesB.Length)
            {
                return true;
            }

            for (int i = 0; i < dependenciesA.Length; i++)
            {
                if (!string.Equals(dependenciesA[i], dependenciesB[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the directory the versions of a package are extracted to.
        /// </summary>
        /// <remarks>
        /// The directory is deleted as a whole when the package goes away, so it must be a
        /// direct child of the extraction root: an empty name would resolve to the root itself -
        /// in a single instance the package directory with every package and the catalog - and
        /// a dot name to the root or above it.
        /// </remarks>
        /// <param name="package">The package.</param>
        /// <returns>The directory, or null when the package file name yields no safe directory.</returns>
        private string GetExtractionDirectory(PackageCatalogItem package)
        {
            var name = Path.GetFileNameWithoutExtension(package?.File ?? string.Empty);
            var root = ExtractionRoot;

            if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.') || string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var directory = Path.GetFullPath(Path.Combine(rootFull, name));

            return string.Equals(Path.GetDirectoryName(directory), rootFull, StringComparison.OrdinalIgnoreCase)
                ? directory
                : null;
        }

        /// <summary>
        /// Removes what was extracted of a package. A directory whose assemblies are still
        /// locked by a load context the runtime has not collected yet is left for the next start.
        /// </summary>
        /// <param name="package">The package.</param>
        private void RemoveExtractedDirectory(PackageCatalogItem package)
        {
            package.ExtractedPath = null;

            var directory = GetExtractionDirectory(package);

            if (directory is null || !Directory.Exists(directory))
            {
                return;
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(directory).ToList())
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _httpServerContext?.Log?.Debug(Translate("extraction.kept", entry));
                }
            }

            try
            {
                Directory.Delete(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // still holds a locked extraction
            }
        }

        /// <summary>
        /// Checks whether a name is a plain package file name in the package directory.
        /// </summary>
        /// <param name="file">The file name.</param>
        /// <returns>True when it is.</returns>
        private static bool IsPackageFileName(string file)
        {
            return !string.IsNullOrWhiteSpace(file)
                && Path.GetFileName(file) == file
                && !file.StartsWith('.')
                && file.EndsWith(".wxp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Copies a file into place so that the target is replaced in one step: a scan reading
        /// the package directory sees either the old or the new file, never a half-written one.
        /// </summary>
        /// <param name="source">The source file.</param>
        /// <param name="target">The target file.</param>
        private static void PlaceFile(string source, string target)
        {
            if (Path.GetFullPath(source).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var temporary = Path.Combine(Path.GetDirectoryName(target), $".{Guid.NewGuid():N}.{Path.GetFileName(target)}.copy");

            try
            {
                File.Copy(source, temporary);
                File.Move(temporary, target, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        /// <summary>
        /// Deletes a file if it exists.
        /// </summary>
        /// <param name="file">The file.</param>
        private static void DeleteFile(string file)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Creates a directory of its own below the staging directory.
        /// </summary>
        /// <returns>The directory.</returns>
        private string CreateStagingDirectory()
        {
            var directory = Path.Combine(_httpServerContext?.PackagePath, StagingDirectory, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            return directory;
        }

        /// <summary>
        /// Removes a directory created by <see cref="CreateStagingDirectory"/>, and the staging
        /// directory with it once it is empty.
        /// </summary>
        /// <param name="directory">The directory.</param>
        private void RemoveStagingDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                var staging = Path.GetDirectoryName(directory);

                if (Directory.Exists(staging) && !Directory.EnumerateFileSystemEntries(staging).Any())
                {
                    Directory.Delete(staging);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // another operation started meanwhile, or a scanner holds a file for a moment;
                // the directory is removed with the stale ones on the next start
                _httpServerContext?.Log?.Debug(Translate("extraction.kept", directory));
            }
        }

        /// <summary>
        /// Removes what operations interrupted by a crash left in the staging directory. Only
        /// old directories go: in a cluster another instance may be uploading right now.
        /// </summary>
        private void RemoveStaleStaging()
        {
            var staging = Path.Combine(_httpServerContext?.PackagePath, StagingDirectory);

            if (!Directory.Exists(staging))
            {
                return;
            }

            foreach (var directory in Directory.GetDirectories(staging)
                .Where(x => DateTime.UtcNow - Directory.GetCreationTimeUtc(x) > StagingLifetime))
            {
                RemoveStagingDirectory(directory);
            }
        }

        /// <summary>
        /// Reads and deserializes the package specification from an archive entry.
        /// </summary>
        /// <param name="specEntry">The spec archive entry.</param>
        /// <returns>The deserialized package spec.</returns>
        private static PackageItemSpec ReadSpec(ZipArchiveEntry specEntry)
        {
            var serializer = new XmlSerializer(typeof(PackageItemSpec));
            using var stream = specEntry.Open();
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });

            return (PackageItemSpec)serializer.Deserialize(xmlReader);
        }

        /// <summary>
        /// Creates a package catalog item from a package specification.
        /// </summary>
        /// <param name="file">The package file path.</param>
        /// <param name="spec">The package specification.</param>
        /// <param name="zip">The package archive, read for the settings files it ships.</param>
        /// <returns>The package catalog item.</returns>
        private static PackageCatalogItem CreateCatalogItem(string file, PackageItemSpec spec, ZipArchive zip)
        {
            if (spec is null)
            {
                return null;
            }

            return new PackageCatalogItem()
            {
                Id = spec.Id,
                File = Path.GetFileName(file),
                State = PackageCatalogItemState.Available,
                Metadata = new PackageItem()
                {
                    FileName = Path.GetFileName(file),
                    Id = spec.Id,
                    Version = spec.Version,
                    Title = spec.Title,
                    Authors = spec.Authors,
                    License = spec.License,
                    Icon = spec.Icon,
                    Readme = spec.Readme,
                    Description = spec.Description,
                    Tags = spec.Tags,
                    PluginSources = spec.Plugins ?? [],
                    Dependencies = spec.Dependencies ?? [],
                    Settings = [.. zip.Entries
                        .Select(x => GetSettingsFileName(x, (x.FullName ?? string.Empty).Replace('\\', '/').TrimStart('/')))
                        .Where(x => x is not null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)]
                }
            };
        }

        /// <summary>
        /// Copies an input stream to an output stream while optionally enforcing a maximum number of bytes.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        /// <param name="maxBytes">The maximum allowed bytes. 0 disables the size check.</param>
        /// <returns>False when the stream exceeds the maximum.</returns>
        private static bool CopyStream(Stream source, Stream target, long maxBytes)
        {
            long totalBytes = 0;
            var buffer = new byte[81920];
            int read;

            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalBytes += read;
                if (maxBytes > 0 && totalBytes > maxBytes)
                {
                    return false;
                }

                target.Write(buffer, 0, read);
            }

            return true;
        }

        /// <summary>
        /// Computes the SHA-256 hash for a file.
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <returns>The SHA-256 hash as lowercase hex string.</returns>
        private static string ComputeSha256(string file)
        {
            using var stream = File.OpenRead(file);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Returns unfulfilled dependency expressions for a package.
        /// </summary>
        /// <param name="package">The package to evaluate.</param>
        /// <returns>The unfulfilled dependency list.</returns>
        private IEnumerable<string> GetUnfulfilledPackageDependencies(PackageCatalogItem package)
        {
            var dependencies = package?.Metadata?.Dependencies ?? [];
            var missing = new List<string>();

            foreach (var dependency in dependencies.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!TryParseDependency(dependency, out string id, out string op, out string requiredVersion))
                {
                    missing.Add(dependency);
                    continue;
                }

                // built-in plugins count as fulfilled dependencies: a package that depends on
                // webexpress.webui must install against a build deployment, where that plugin is
                // referenced statically and therefore never appears in the catalog. a dependency
                // on a plugin of an installed package is fulfilled by that package
                var dependencyPackage = GetPackage(id)
                    ?? Catalog.Packages.FirstOrDefault(x => x is not null && x != package && GetIdentities(x).Contains(id));

                // only a running package provides its types; one that waits or is disabled does not
                if (dependencyPackage is null || dependencyPackage == package || dependencyPackage.State != PackageCatalogItemState.Active)
                {
                    missing.Add(dependency);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(op))
                {
                    var currentVersion = dependencyPackage.Metadata?.Version;
                    if (!IsVersionConstraintSatisfied(currentVersion, op, requiredVersion))
                    {
                        missing.Add(dependency);
                    }
                }
            }

            return missing;
        }

        /// <summary>
        /// Parses a dependency expression.
        /// </summary>
        /// <param name="expression">The dependency expression (e.g. "pkg>=1.0.0").</param>
        /// <param name="id">The dependency id.</param>
        /// <param name="op">The comparison operator.</param>
        /// <param name="version">The version constraint.</param>
        /// <returns>True if parsing was successful; otherwise false.</returns>
        private static bool TryParseDependency(string expression, out string id, out string op, out string version)
        {
            id = null;
            op = null;
            version = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            var value = expression.Trim();
            var operators = new[] { ">=", "<=", "==", "=", ">", "<" };
            var index = -1;
            var selectedOperator = string.Empty;

            foreach (var candidate in operators)
            {
                index = value.IndexOf(candidate, StringComparison.Ordinal);
                if (index > 0)
                {
                    selectedOperator = candidate;
                    break;
                }
            }

            if (index < 0)
            {
                id = value;
                return !string.IsNullOrWhiteSpace(id);
            }

            id = value[..index].Trim();
            op = selectedOperator;
            version = value[(index + selectedOperator.Length)..].Trim();

            return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(op) && !string.IsNullOrWhiteSpace(version);
        }

        /// <summary>
        /// Evaluates whether a version satisfies a version constraint.
        /// </summary>
        /// <param name="currentVersion">The current version.</param>
        /// <param name="op">The operator.</param>
        /// <param name="requiredVersion">The required version.</param>
        /// <returns>True if the constraint is satisfied; otherwise false.</returns>
        private static bool IsVersionConstraintSatisfied(string currentVersion, string op, string requiredVersion)
        {
            if (!TryParseVersion(currentVersion, out var current) || !TryParseVersion(requiredVersion, out var required))
            {
                return false;
            }

            var compare = current.CompareTo(required);

            return op switch
            {
                ">" => compare > 0,
                ">=" => compare >= 0,
                "<" => compare < 0,
                "<=" => compare <= 0,
                "=" => compare == 0,
                "==" => compare == 0,
                _ => false
            };
        }

        /// <summary>
        /// Parses a version string with support for prerelease/build suffixes.
        /// </summary>
        /// <param name="value">The version string.</param>
        /// <param name="version">The parsed version.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            var suffixIndex = normalized.IndexOfAny(['-', '+']);
            if (suffixIndex >= 0)
            {
                normalized = normalized[..suffixIndex];
            }

            return Version.TryParse(normalized, out version);
        }

        /// <summary>
        /// Translates a text of the package manager.
        /// </summary>
        /// <param name="key">The key below <c>webexpress.webcore:packagemanager.</c>.</param>
        /// <param name="args">The arguments of the text.</param>
        /// <returns>The text in the culture of the server.</returns>
        private static string Translate(string key, params object[] args)
        {
            return I18N.Translate($"webexpress.webcore:packagemanager.{key}", args);
        }

        /// <summary>
        /// Creates a message of the package manager that is translated only when it is read.
        /// </summary>
        /// <param name="key">The key below the package manager's internationalization prefix.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The message.</returns>
        private static PackageText Text(string key, params object[] args)
        {
            return new PackageText(key, args);
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            _clusterSubscription?.Dispose();
            ShutDown();
        }

        /// <summary>
        /// Returns the cluster manager while several instances share the package directory, or
        /// null for a single instance.
        /// </summary>
        private IClusterManager Cluster => _componentHub?.ClusterManager is { IsClustered: true } cluster ? cluster : null;

        /// <summary>
        /// Returns the directory packages are extracted to. A single instance extracts next to the
        /// packages. Instances of a cluster share the package directory, so each extracts into a
        /// directory of its own: several instances extracting into one directory would overwrite
        /// assemblies another instance has loaded, and removing a package on one instance would
        /// pull them from under the others. Decided once, before the first package is extracted.
        /// </summary>
        private string ExtractionRoot => _extractionRoot ??= Cluster is { } cluster
            ? Path.Combine(Path.GetTempPath(), "webexpress", "packages", string.Concat(cluster.NodeId.Select(x => char.IsLetterOrDigit(x) || x is '-' or '.' ? x : '_')))
            : _httpServerContext?.PackagePath;

        /// <summary>
        /// Runs a change of the package set so that no other instance changes the shared catalog
        /// at the same time, and announces it to the other instances afterwards.
        /// </summary>
        /// <param name="operation">The change.</param>
        /// <returns>The result of the change.</returns>
        private PackageOperationResult Coordinate(Func<PackageOperationResult> operation)
        {
            lock (_scanLock)
            {
                var cluster = Cluster;

                if (cluster is null)
                {
                    return operation();
                }

                using var coordination = cluster.Lock(ClusterLock, ClusterLockLifetime, ClusterLockTimeout);

                if (coordination is null)
                {
                    return PackageOperationResult.Failed(Text("cluster.busy"));
                }

                // the change builds on what the other instances decided so far, not on this
                // instance's possibly older view
                ScanCore(sharedOnly: true);

                var result = operation();

                if (result.Success)
                {
                    _ = cluster.PublishAsync(ClusterTopic, []);
                }

                return result;
            }
        }

        /// <summary>
        /// Reads the states other instances recorded in the shared catalog.
        /// </summary>
        /// <returns>The recorded states, or null when there is no readable catalog.</returns>
        private SharedStates ReadSharedStates()
        {
            var catalogeFile = Path.Combine(_httpServerContext?.PackagePath, "catalog.xml");

            try
            {
                using var stream = new FileStream(catalogeFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                if (stream.Length == 0)
                {
                    return null;
                }

                var catalog = (PackageCatalog)new XmlSerializer(typeof(PackageCatalog)).Deserialize(stream);
                var states = new SharedStates();

                foreach (var item in catalog?.Packages?.Where(x => x?.File is not null) ?? [])
                {
                    states.Packages[item.File] = item.State;
                    states.InstalledSettings[item.File] = item.InstalledSettings;
                }

                states.DisabledPlugins.UnionWith(catalog?.DisabledPlugins?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? []);
                states.RemovedPlugins.UnionWith(catalog?.RemovedPlugins?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? []);

                return states;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // no catalog yet, or one being replaced; the next scan reads it again. nothing is
                // adopted from it - an unreadable list of disabled plugins is not an empty one
                return null;
            }
        }

        /// <summary>
        /// Brings the packages of this instance in line with the states another instance recorded:
        /// a package deactivated there is unloaded here, one activated there becomes available
        /// here and starts with the next round of pending packages. The same holds for the
        /// built-in plugins.
        /// </summary>
        /// <param name="shared">The recorded states, or null when none could be read.</param>
        /// <returns>True when a package changed state.</returns>
        private bool AdoptSharedStates(SharedStates shared)
        {
            if (shared is null)
            {
                return false;
            }

            var changed = false;

            foreach (var package in Catalog.Packages.Where(x => x?.File is not null).ToList())
            {
                if (shared.InstalledSettings.TryGetValue(package.File, out var installedSettings))
                {
                    package.InstalledSettings = [.. installedSettings];
                }

                if (shared.Packages.TryGetValue(package.File, out var state) && state != package.State)
                {
                    changed |= AdoptState(package, state == PackageCatalogItemState.Disable);
                }
            }

            foreach (var package in GetBuiltInPackages(Catalog.Packages.Where(x => x is not null)).Where(x => !x.SystemPlugin))
            {
                var file = Path.GetFileName(package.Assembly?.Location ?? string.Empty);

                // uninstalled on another instance; this one deletes its copy on its next start
                if (shared.RemovedPlugins.Contains(file))
                {
                    UnloadDependents(package);
                    Unload(package);
                    _builtIns.Remove(package.Id);
                    Catalog.RemovedPlugins.Add(file);
                    OnRemovePackage(package);
                    changed = true;

                    continue;
                }

                changed |= AdoptState(package, shared.DisabledPlugins.Contains(package.Id));
            }

            return changed;
        }

        /// <summary>
        /// Switches a package on or off as another instance decided.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="disabled">True when the package was deactivated there.</param>
        /// <returns>True when the package changed state.</returns>
        private bool AdoptState(PackageCatalogItem package, bool disabled)
        {
            if (disabled && package.State != PackageCatalogItemState.Disable)
            {
                UnloadDependents(package);
                Unload(package);
                package.State = PackageCatalogItemState.Disable;
                package.Faulted = false;
                package.ProblemText = null;

                return true;
            }

            if (!disabled && package.State == PackageCatalogItemState.Disable)
            {
                package.State = PackageCatalogItemState.Available;
                package.Faulted = false;

                return true;
            }

            return false;
        }

        /// <summary>
        /// The states the instances of a cluster recorded in the shared catalog.
        /// </summary>
        private sealed class SharedStates
        {
            /// <summary>
            /// Gets the state of each package, by package file.
            /// </summary>
            public Dictionary<string, PackageCatalogItemState> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets the settings files each package actually deployed.
            /// </summary>
            public Dictionary<string, List<string>> InstalledSettings { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets the ids of the deactivated built-in plugins.
            /// </summary>
            public HashSet<string> DisabledPlugins { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets the assembly file names of the uninstalled built-in plugins.
            /// </summary>
            public HashSet<string> RemovedPlugins { get; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
