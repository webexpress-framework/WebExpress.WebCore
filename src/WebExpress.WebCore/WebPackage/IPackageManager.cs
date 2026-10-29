using System;
using System.IO;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPackage.Model;

namespace WebExpress.WebCore.WebPackage
{
    /// <summary>
    /// The package manager manages packages with WebExpress extensions. The packages must 
    /// be in WebExpressPackage format (*.wxp).
    /// </summary>
    public interface IPackageManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an package is added.
        /// </summary>
        event EventHandler<PackageCatalogItem> AddPackage;

        /// <summary>
        /// An event that fires when an package is removed.
        /// </summary>
        event EventHandler<PackageCatalogItem> RemovePackage;

        /// <summary>
        /// Gets the catalog of installed packages.
        /// </summary>
        PackageCatalog Catalog { get; }

        /// <summary>
        /// Returns all package entries, the installed ones and the plugins that ship with the
        /// application.
        /// </summary>
        /// <returns>An enumerable collection with all package entries.</returns>
        IEnumerable<PackageCatalogItem> GetPackages();

        /// <summary>
        /// Returns a package by id.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The package or null.</returns>
        PackageCatalogItem GetPackage(string packageId);

        /// <summary>
        /// Returns the active packages that depend on a package, directly or through one of
        /// their plugins. As long as there are any, the package can be neither deactivated nor
        /// uninstalled: the dependents run code of its assemblies.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The dependent packages, empty when there are none or the package is unknown.</returns>
        IEnumerable<PackageCatalogItem> GetDependentPackages(string packageId);

        /// <summary>
        /// Validates a package file.
        /// </summary>
        /// <param name="packageFile">The package file path.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The validation result.</returns>
        PackageValidationResult ValidatePackage(string packageFile, long maxPackageBytes = 0, string expectedSha256 = null);

        /// <summary>
        /// Uploads and installs a package from a stream.
        /// </summary>
        /// <param name="packageStream">The package stream.</param>
        /// <param name="fileName">The package file name.</param>
        /// <param name="activate">True to activate directly after install; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        PackageOperationResult UploadPackage(Stream packageStream, string fileName, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null);

        /// <summary>
        /// Installs a package from a file.
        /// </summary>
        /// <param name="packageFile">The package file path.</param>
        /// <param name="activate">True to activate directly after install; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        PackageOperationResult InstallPackage(string packageFile, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null);

        /// <summary>
        /// Activates a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        PackageOperationResult ActivatePackage(string packageId);

        /// <summary>
        /// Deactivates a package or a plugin shipping with the application.
        /// </summary>
        /// <remarks>
        /// The active packages and plugins that run on it are stopped with it. They are not
        /// deactivated themselves but wait, and start again once it is activated. A system plugin
        /// cannot be deactivated.
        /// </remarks>
        /// <param name="packageId">The package id.</param>
        /// <returns>The operation result.</returns>
        PackageOperationResult DeactivatePackage(string packageId);

        /// <summary>
        /// Updates a package from a file path.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageFile">The package file path.</param>
        /// <param name="activate">True to activate directly after update; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        PackageOperationResult UpdatePackage(string packageId, string packageFile, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null);

        /// <summary>
        /// Updates a package from an uploaded stream. The upload is checked before it replaces
        /// anything, and a new version that does not start is rolled back to the previous one.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageStream">The package stream.</param>
        /// <param name="fileName">The package file name.</param>
        /// <param name="activate">True to activate directly after update; false to keep it disabled.</param>
        /// <param name="maxPackageBytes">Optional max allowed package size in bytes. 0 disables the limit check.</param>
        /// <param name="expectedSha256">Optional expected SHA-256 hash in hex format.</param>
        /// <returns>The operation result.</returns>
        PackageOperationResult UpdatePackage(string packageId, Stream packageStream, string fileName, bool activate = true, long maxPackageBytes = 0, string expectedSha256 = null);

        /// <summary>
        /// Uninstalls and removes a package.
        /// </summary>
        /// <remarks>
        /// The settings files a package deployed belong to the administrator once they are in
        /// the settings directory, so they are kept unless removing them is asked for explicitly.
        /// </remarks>
        /// <param name="packageId">The package id.</param>
        /// <param name="removeSettings">True to also delete the settings files the package ships.</param>
        /// <returns>The operation result.</returns>
        PackageOperationResult UninstallPackage(string packageId, bool removeSettings = false);
    }
}
