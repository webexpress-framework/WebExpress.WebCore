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
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPackage.Model;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebPackage
{
    /// <summary>
    /// The package manager manages packages with WebExpress extensions. The packages 
    /// must be in WebExpressPackage format (*.wxp).
    /// </summary>
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
            // load the default plugins
            _pluginManager.Register();

            // boot default elements 
            _componentHub?.BootComponent(_pluginManager.Plugins);

            LoadCatalog();

            foreach (var package in Catalog.Packages)
            {
                var packagesFromFile = LoadPackage(Path.Combine(_httpServerContext?.PackagePath, package.File));

                package.Metadata = packagesFromFile?.Metadata;

                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate("webexpress.webcore:packagemanager.existing", package.File)
                );

                if (package.State != PackageCatalogeItemState.Disable)
                {
                    package.State = PackageCatalogeItemState.Active;
                    ExtractPackage(package);
                    RegisterPackage(package);
                    BootPackage(package);
                }
            }

            SaveCatalog();

            // build sitemap
            _componentHub?.SitemapManager.Refresh();

            Task.Factory.StartNew(() =>
            {
                while (!TokenSource.IsCancellationRequested)
                {
                    Scan();

                    var secendsLeft = 60 - DateTime.Now.Second;
                    Thread.Sleep(secendsLeft * 1000);
                }

            }, TokenSource.Token);
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
                _httpServerContext?.Log?.Debug
                (
                    I18N.Translate
                    (
                        "webexpress.webcore:packagemanager.scan",
                        _httpServerContext?.PackagePath
                    )
                );

                // determine all WebExpress packages from the file system
                var packageFiles = Directory.GetFiles(_httpServerContext?.PackagePath, "*.wxp").Select(x => Path.GetFileName(x)).ToList();

                // all packages that are not yet installed
                var newPackages = packageFiles.Except(Catalog.Packages.Where(x => x is not null).Select(x => x.File)).ToList();

                // all packages that are no longer available
                var removePackages = Catalog.Packages.Where(x => x is not null).Select(x => x.File).Except(packageFiles).ToList();

                // determine changed packages by comparing spec version and relevant metadata
                var changedPackages = new List<string>();
                foreach (var existing in Catalog.Packages.Where(x => x is not null))
                {
                    var fullPath = Path.Combine(_httpServerContext?.PackagePath, existing.File);
                    if (!File.Exists(fullPath))
                    {
                        continue;
                    }

                    var fromFile = LoadPackage(fullPath);
                    if (fromFile is null)
                    {
                        continue;
                    }

                    if (HasPackageChanged(existing, fromFile))
                    {
                        changedPackages.Add(existing.File);
                    }
                }

                foreach (var package in newPackages)
                {
                    var packagesFromFile = LoadPackage(Path.Combine(_httpServerContext?.PackagePath, package));
                    if (packagesFromFile is null)
                    {
                        continue;
                    }

                    packagesFromFile.State = PackageCatalogeItemState.Active;

                    ExtractPackage(packagesFromFile);
                    RegisterPackage(packagesFromFile);
                    BootPackage(packagesFromFile);

                    Catalog.Packages.Add(packagesFromFile);

                    // raise event for added package
                    OnAddPackage(packagesFromFile);

                    _httpServerContext?.Log?.Debug
                    (
                        I18N.Translate
                        (
                            "webexpress.webcore:packagemanager.add",
                            package
                        )
                    );
                }

                foreach (var package in changedPackages)
                {
                    var existing = Catalog.Packages.FirstOrDefault(x => x is not null && x.File == package);
                    if (existing is null)
                    {
                        continue;
                    }

                    var fromFile = LoadPackage(Path.Combine(_httpServerContext?.PackagePath, package));
                    if (fromFile is null)
                    {
                        continue;
                    }

                    // respect disabled state; only update metadata without activating
                    if (existing.State == PackageCatalogeItemState.Disable)
                    {
                        existing.Metadata = fromFile.Metadata;
                        _httpServerContext?.Log?.Debug($"package '{package}' metadata updated while disabled");
                    }
                    else
                    {
                        // deactivate and unload old plugin instances
                        DeactivateAndUnregisterPackage(existing);
                        // cleanup extracted content
                        RemoveExtractedDirectory(existing);

                        // update metadata and identification
                        existing.Id = fromFile.Id;
                        existing.Metadata = fromFile.Metadata;
                        existing.State = PackageCatalogeItemState.Active;

                        // extract, register and boot new content
                        ExtractPackage(existing);
                        RegisterPackage(existing);
                        BootPackage(existing);

                        _httpServerContext?.Log?.Debug($"package '{package}' updated and reloaded");
                    }
                }

                foreach (var package in removePackages)
                {
                    var existing = Catalog.Packages.FirstOrDefault(x => x is not null && x.File == package);
                    if (existing is null)
                    {
                        continue;
                    }

                    // deactivate and unload all plugins related to the package
                    DeactivateAndUnregisterPackage(existing);

                    // cleanup extracted directory
                    RemoveExtractedDirectory(existing);

                    // raise event before removing from catalog
                    OnRemovePackage(existing);

                    // remove package from catalog
                    Catalog.Packages.Remove(existing);

                    _httpServerContext?.Log?.Debug
                    (
                        I18N.Translate
                        (
                            "webexpress.webcore:packagemanager.remove",
                            package
                        )
                    );
                }

                if (newPackages.Count != 0 || removePackages.Count != 0 || changedPackages.Count != 0)
                {
                    // build sitemap
                    _componentHub?.SitemapManager.Refresh();

                    // save the catalog
                    SaveCatalog();
                }
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
        /// Builds a catalog entry for every registered plugin that no catalog entry accounts for.
        /// </summary>
        /// <remarks>
        /// A plugin is covered when a catalog entry lists it among its own plugins, or when a
        /// catalog entry carries its id - so a plugin that is present statically and as a package
        /// is reported once, by the package, which is the entry the operations can act on.
        /// </remarks>
        /// <param name="packages">The installed packages the plugins are matched against.</param>
        /// <returns>The synthesized entries, ordered by id.</returns>
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

            return (_pluginManager?.Plugins ?? [])
                .Where(x => x?.PluginId is not null && !covered.Contains(x.PluginId.ToString()))
                .Select(CreateBuiltInItem)
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Creates the catalog entry that stands for a plugin loaded from the application directory.
        /// </summary>
        /// <param name="plugin">The plugin context.</param>
        /// <returns>The synthesized catalog entry.</returns>
        private static PackageCatalogItem CreateBuiltInItem(IPluginContext plugin)
        {
            var id = plugin.PluginId.ToString();

            return new PackageCatalogItem()
            {
                Id = id,
                // there is no package file behind a built-in plugin. the empty name is deliberate:
                // every path built from it fails to resolve, which is the second line of defence
                // behind the guards on the operations
                File = string.Empty,
                State = PackageCatalogeItemState.Active,
                BuiltIn = true,
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
                    Dependencies = []
                }
            };
        }

        /// <summary>
        /// Rejects an operation that was asked to modify a plugin shipping with the application.
        /// </summary>
        /// <remarks>
        /// None of the package operations can be carried out on such a plugin: its assembly lives
        /// in the application directory, is loaded into the default context and cannot be replaced
        /// or removed while the process runs. Failing up front is what keeps a request from leaving
        /// the plugin half unregistered.
        /// </remarks>
        /// <param name="package">The package the operation was addressed to.</param>
        /// <param name="operation">The name of the operation, used in the message.</param>
        /// <returns>The failure result, or null when the package may be operated on.</returns>
        private static PackageOperationResult RejectBuiltIn(PackageCatalogItem package, string operation)
        {
            return package is not null && package.BuiltIn
                ? PackageOperationResult.Failed($"Package '{package.Id}' ships with the application and cannot be {operation}.", package)
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
                result.Messages.Add("The package path is empty.");
                return result;
            }

            if (!File.Exists(packageFile))
            {
                result.Messages.Add("The package file does not exist.");
                return result;
            }

            if (!Path.GetExtension(packageFile).Equals(".wxp", StringComparison.OrdinalIgnoreCase))
            {
                result.Messages.Add("The package file extension must be '.wxp'.");
                return result;
            }

            if (maxPackageBytes > 0)
            {
                var fileInfo = new FileInfo(packageFile);
                if (fileInfo.Length > maxPackageBytes)
                {
                    result.Messages.Add($"The package size exceeds the allowed limit ({maxPackageBytes} bytes).");
                    return result;
                }
            }

            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                var hash = ComputeSha256(packageFile);
                if (!hash.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    result.Messages.Add("The package signature/hash verification failed.");
                    return result;
                }
            }

            try
            {
                using var zip = ZipFile.OpenRead(packageFile);
                var specEntry = zip.Entries.FirstOrDefault(x => Path.GetExtension(x.FullName).Equals(".spec", StringComparison.OrdinalIgnoreCase));
                if (specEntry is null)
                {
                    result.Messages.Add("The package does not contain a .spec file.");
                    return result;
                }

                var spec = ReadSpec(specEntry);
                if (spec is null)
                {
                    result.Messages.Add("The package specification could not be read.");
                    return result;
                }

                if (string.IsNullOrWhiteSpace(spec.Id))
                {
                    result.Messages.Add("The package id is missing in the .spec file.");
                }

                if (string.IsNullOrWhiteSpace(spec.Version))
                {
                    result.Messages.Add("The package version is missing in the .spec file.");
                }

                foreach (var plugin in spec.Plugins ?? [])
                {
                    if (string.IsNullOrWhiteSpace(plugin))
                    {
                        result.Messages.Add("A plugin entry in the .spec file is empty.");
                        continue;
                    }

                    if (Path.IsPathRooted(plugin))
                    {
                        result.Messages.Add($"The plugin entry '{plugin}' must be a relative path.");
                    }

                    var normalized = plugin.Replace('\\', '/');
                    if (normalized.Contains("..", StringComparison.Ordinal))
                    {
                        result.Messages.Add($"The plugin entry '{plugin}' contains invalid traversal segments.");
                    }
                }

                result.Package = CreateCatalogItem(packageFile, spec);
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
                result.Messages.Add("The package archive is invalid or corrupted.");
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
            if (packageStream is null)
            {
                return PackageOperationResult.Failed("The upload stream is null.");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return PackageOperationResult.Failed("The upload file name is empty.");
            }

            var safeFileName = Path.GetFileName(fileName);
            if (!safeFileName.EndsWith(".wxp", StringComparison.OrdinalIgnoreCase))
            {
                return PackageOperationResult.Failed("The upload file extension must be '.wxp'.");
            }

            var tmpFile = Path.Combine(_httpServerContext?.PackagePath, $"{Guid.NewGuid()}.{safeFileName}");
            Directory.CreateDirectory(_httpServerContext?.PackagePath);

            try
            {
                using (var fileStream = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CopyStream(packageStream, fileStream, maxPackageBytes);
                }

                var targetFile = Path.Combine(_httpServerContext?.PackagePath, safeFileName);
                File.Copy(tmpFile, targetFile, true);

                return InstallPackage(targetFile, activate, maxPackageBytes, expectedSha256);
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
                return PackageOperationResult.Failed("The package upload failed.");
            }
            finally
            {
                if (File.Exists(tmpFile))
                {
                    File.Delete(tmpFile);
                }
            }
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
            lock (_scanLock)
            {
                var validation = ValidatePackage(packageFile, maxPackageBytes, expectedSha256);
                if (!validation.IsValid)
                {
                    return PackageOperationResult.Failed(string.Join(" ", validation.Messages));
                }

                var package = validation.Package;
                var safeFile = Path.GetFileName(packageFile);
                var targetFile = Path.Combine(_httpServerContext?.PackagePath, safeFile);
                Directory.CreateDirectory(_httpServerContext?.PackagePath);

                if (!Path.GetFullPath(packageFile).Equals(Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(packageFile, targetFile, true);
                }

                package.File = safeFile;

                var existing = Catalog.Packages
                    .FirstOrDefault(x => x is not null && x.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    Catalog.Packages.Add(package);
                    existing = package;
                    OnAddPackage(existing);
                }
                else
                {
                    var oldPackageFile = Path.Combine(_httpServerContext?.PackagePath, existing.File);
                    DeactivateAndUnregisterPackage(existing);
                    RemoveExtractedDirectory(existing);

                    existing.File = package.File;
                    existing.Metadata = package.Metadata;

                    if (!oldPackageFile.Equals(targetFile, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPackageFile))
                    {
                        File.Delete(oldPackageFile);
                    }
                }

                if (!activate)
                {
                    existing.State = PackageCatalogeItemState.Disable;
                    SaveCatalog();
                    _componentHub?.SitemapManager.Refresh();
                    return PackageOperationResult.Ok($"Package '{existing.Id}' installed (disabled).", existing);
                }

                var activateResult = ActivatePackage(existing.Id);
                return activateResult.Success
                    ? PackageOperationResult.Ok($"Package '{existing.Id}' installed and activated.", existing)
                    : activateResult;
            }
        }

        /// <summary>
        /// Activates a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult ActivatePackage(string packageId)
        {
            lock (_scanLock)
            {
                var package = GetPackage(packageId);
                if (package is null)
                {
                    return PackageOperationResult.Failed($"Package '{packageId}' was not found.");
                }

                var builtIn = RejectBuiltIn(package, "activated");
                if (builtIn is not null)
                {
                    return builtIn;
                }

                if (package.State == PackageCatalogeItemState.Active)
                {
                    return PackageOperationResult.Ok($"Package '{packageId}' is already active.", package);
                }

                var missingDependencies = GetUnfulfilledPackageDependencies(package).ToList();
                if (missingDependencies.Count > 0)
                {
                    package.State = PackageCatalogeItemState.Disable;
                    SaveCatalog();
                    return PackageOperationResult.Failed($"Package '{packageId}' has missing dependencies: {string.Join(", ", missingDependencies)}", package);
                }

                DeactivateAndUnregisterPackage(package);
                RemoveExtractedDirectory(package);

                ExtractPackage(package);
                RegisterPackage(package);
                BootPackage(package);
                package.State = PackageCatalogeItemState.Active;

                SaveCatalog();
                _componentHub?.SitemapManager.Refresh();

                return PackageOperationResult.Ok($"Package '{packageId}' activated.", package);
            }
        }

        /// <summary>
        /// Deactivates a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult DeactivatePackage(string packageId)
        {
            lock (_scanLock)
            {
                var package = GetPackage(packageId);
                if (package is null)
                {
                    return PackageOperationResult.Failed($"Package '{packageId}' was not found.");
                }

                var builtIn = RejectBuiltIn(package, "deactivated");
                if (builtIn is not null)
                {
                    return builtIn;
                }

                DeactivateAndUnregisterPackage(package);
                RemoveExtractedDirectory(package);
                package.State = PackageCatalogeItemState.Disable;

                SaveCatalog();
                _componentHub?.SitemapManager.Refresh();

                return PackageOperationResult.Ok($"Package '{packageId}' deactivated.", package);
            }
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
            var builtIn = RejectBuiltIn(GetPackage(packageId), "updated");
            if (builtIn is not null)
            {
                return builtIn;
            }

            var validation = ValidatePackage(packageFile, maxPackageBytes, expectedSha256);
            if (!validation.IsValid)
            {
                return PackageOperationResult.Failed(string.Join(" ", validation.Messages));
            }

            if (validation.Package is null || !validation.Package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return PackageOperationResult.Failed($"The uploaded package id does not match '{packageId}'.");
            }

            return InstallPackage(packageFile, activate, maxPackageBytes, expectedSha256);
        }

        /// <summary>
        /// Uninstalls and removes a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        public PackageOperationResult UninstallPackage(string packageId)
        {
            lock (_scanLock)
            {
                var package = GetPackage(packageId);
                if (package is null)
                {
                    return PackageOperationResult.Failed($"Package '{packageId}' was not found.");
                }

                var builtIn = RejectBuiltIn(package, "uninstalled");
                if (builtIn is not null)
                {
                    return builtIn;
                }

                DeactivateAndUnregisterPackage(package);
                RemoveExtractedDirectory(package);

                var packageFile = Path.Combine(_httpServerContext?.PackagePath, package.File);
                if (File.Exists(packageFile))
                {
                    File.Delete(packageFile);
                }

                Catalog.Packages.Remove(package);
                OnRemovePackage(package);

                SaveCatalog();
                _componentHub?.SitemapManager.Refresh();

                return PackageOperationResult.Ok($"Package '{packageId}' uninstalled.", package);
            }
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
                    return CreateCatalogItem(file, spec);
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
                using var catalog = new StreamReader(catalogeFile);

                if (catalog.BaseStream.Length == 0)
                {
                    return;
                }

                var serializer = new XmlSerializer(typeof(PackageCatalog));
                var items = (PackageCatalog)serializer.Deserialize(catalog);

                Catalog.Packages.Clear();
                //Catalog.Packages.RemoveAll(x => !x.System);
                Catalog.Packages.AddRange(items.Packages);
            }

            Log();
        }

        /// <summary>
        /// Save the catalog.
        /// </summary>
        private void SaveCatalog()
        {
            var catalogeFile = Path.Combine(_httpServerContext?.PackagePath, "catalog.xml");

            using var fs = new FileStream(catalogeFile, FileMode.Create);
            using var writer = new XmlTextWriter(fs, Encoding.Unicode);
            var serializer = new XmlSerializer(typeof(PackageCatalog));

            writer.Formatting = Formatting.Indented;
            serializer.Serialize(writer, Catalog, new XmlSerializerNamespaces([new XmlQualifiedName("", "")]));

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:packagemanager.save")
            );
        }

        /// <summary>
        /// Extracts the specified package to the file system.
        /// </summary>
        /// <param name="package">The package.</param>
        private void ExtractPackage(PackageCatalogItem package)
        {
            var packageFile = Path.Combine(_httpServerContext?.PackagePath, package?.File);

            if (File.Exists(packageFile))
            {
                using var zip = ZipFile.Open(packageFile, ZipArchiveMode.Read);

                var extractedPath = Path.Combine(_httpServerContext?.PackagePath, Path.GetFileNameWithoutExtension(package?.File));
                var extractedPathFull = Path.GetFullPath(extractedPath);

                if (!Directory.Exists(extractedPath))
                {
                    Directory.CreateDirectory(extractedPath);
                }

                var deployedSettings = false;

                foreach (var entry in zip.Entries)
                {
                    var normalized = (entry.FullName ?? string.Empty).Replace('\\', '/').TrimStart('/');

                    if (normalized.StartsWith(PackageBuilder.SettingsDirectory + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        deployedSettings |= DeploySettings(entry, normalized);

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
        }

        /// <summary>
        /// Deploys a settings file of a package to the settings directory of the server. An
        /// existing file is left alone: it is the administrator's by then, and a package update
        /// must not undo the changes made to it.
        /// </summary>
        /// <param name="entry">The archive entry of the settings file.</param>
        /// <param name="normalized">The entry path with forward slashes and no leading slash.</param>
        /// <returns><see langword="true"/> when the file was written, <see langword="false"/> when it was skipped.</returns>
        private bool DeploySettings(ZipArchiveEntry entry, string normalized)
        {
            var settingsPath = _httpServerContext?.SettingsPath;
            var segments = normalized.Split('/');

            // directory entries carry no file
            if (string.IsNullOrEmpty(entry.Name) || string.IsNullOrWhiteSpace(settingsPath))
            {
                return false;
            }

            // only json files directly below the settings directory are settings; a nested path
            // could escape the directory and another extension would never be merged
            if (segments.Length != 2
                || !segments[1].EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || segments[1].IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:packagemanager.settings.ignored", entry.FullName));

                return false;
            }

            var targetFilePath = Path.Combine(settingsPath, segments[1]);

            if (File.Exists(targetFilePath))
            {
                _httpServerContext?.Log?.Debug(I18N.Translate("webexpress.webcore:packagemanager.settings.existing", segments[1]));

                return false;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var content = buffer.ToArray();

            // a file that does not parse is refused before it reaches the directory: once there,
            // it would fail every following start of the server, not just this installation
            if (!IsJson(content))
            {
                _httpServerContext?.Log?.Warning(I18N.Translate("webexpress.webcore:packagemanager.settings.invalid", entry.FullName));

                return false;
            }

            Directory.CreateDirectory(settingsPath);
            File.WriteAllBytes(targetFilePath, content);

            _httpServerContext?.Log?.Info(I18N.Translate("webexpress.webcore:packagemanager.settings.deployed", segments[1]));

            return true;
        }

        /// <summary>
        /// Checks whether the content is a json document the configuration would accept: comments
        /// and trailing commas included, as the json configuration provider allows them too.
        /// </summary>
        /// <param name="content">The content to check.</param>
        /// <returns><see langword="true"/> when the content parses as json.</returns>
        private static bool IsJson(byte[] content)
        {
            try
            {
                using var document = JsonDocument.Parse(content, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Registers the plungins included in the package.
        /// </summary>
        /// <param name="package">The package.</param>
        private void RegisterPackage(PackageCatalogItem package)
        {
            // load plugins
            foreach (var plugin in package?.Metadata?.PluginSources ?? [])
            {
                var pluginContexts = _pluginManager.Register(GetTargetPath(package, plugin));

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
        private string GetTargetPath(PackageCatalogItem package, string plugin)
        {
            return Path.GetFullPath(Path.Combine
            (
                _httpServerContext?.PackagePath,
                Path.GetFileNameWithoutExtension(package?.File), plugin, GetTFM(), $"{Path.GetFileName(plugin)}.dll"
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
            if (existing is null || fromFile is null)
            {
                return false;
            }

            // if no metadata was present, treat as no change and let metadata be assigned on next run
            if (existing.Metadata is null || fromFile.Metadata is null)
            {
                return false;
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
        /// Gracefully deactivates a package, shutting down and removing its plugins and 
        /// clearing the plugin list.
        /// </summary>
        /// <param name="package">The package.</param>
        private void DeactivateAndUnregisterPackage(PackageCatalogItem package)
        {
            if (package is null)
            {
                return;
            }

            // shut down components associated to each plugin and remove the plugin
            foreach (var pluginContext in package.Plugins.ToList())
            {
                _componentHub?.ShutDownComponent(pluginContext);
                _pluginManager.Remove(pluginContext);
            }

            package.Plugins.Clear();
            package.State = PackageCatalogeItemState.Available;
        }

        /// <summary>
        /// Removes the extracted directory for a package if it exists.
        /// </summary>
        /// <param name="package">The package.</param>
        private void RemoveExtractedDirectory(PackageCatalogItem package)
        {
            var extractedPath = Path.Combine(_httpServerContext?.PackagePath, Path.GetFileNameWithoutExtension(package?.File));
            try
            {
                if (Directory.Exists(extractedPath))
                {
                    Directory.Delete(extractedPath, true);
                }
            }
            catch (Exception ex)
            {
                // keep running even if cleanup fails
                _httpServerContext?.Log?.Exception(ex);
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
        /// <returns>The package catalog item.</returns>
        private static PackageCatalogItem CreateCatalogItem(string file, PackageItemSpec spec)
        {
            if (spec is null)
            {
                return null;
            }

            return new PackageCatalogItem()
            {
                Id = spec.Id,
                File = Path.GetFileName(file),
                State = PackageCatalogeItemState.Available,
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
                    Dependencies = spec.Dependencies ?? []
                }
            };
        }

        /// <summary>
        /// Copies an input stream to an output stream while optionally enforcing a maximum number of bytes.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        /// <param name="maxBytes">The maximum allowed bytes. 0 disables the size check.</param>
        private static void CopyStream(Stream source, Stream target, long maxBytes)
        {
            long totalBytes = 0;
            var buffer = new byte[81920];
            int read;

            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalBytes += read;
                if (maxBytes > 0 && totalBytes > maxBytes)
                {
                    throw new InvalidOperationException($"The uploaded package exceeds the allowed size ({maxBytes} bytes).");
                }

                target.Write(buffer, 0, read);
            }
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
                // referenced statically and therefore never appears in the catalog
                var dependencyPackage = GetPackage(id);

                if (dependencyPackage is null || dependencyPackage.State == PackageCatalogeItemState.Disable)
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
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
