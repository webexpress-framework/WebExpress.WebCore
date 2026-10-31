namespace WebExpress.WebCore.WebPackage.Model
{
    /// <summary>
    /// Lifecycle state of a package listed in the catalog: present but not yet loaded
    /// (<c>Available</c>), loaded and usable (<c>Active</c>), or switched off (<c>Disable</c>).
    /// </summary>
    public enum PackageCatalogItemState
    {
        /// <summary>
        /// The package is available but has not yet been loaded by WebExpress.
        /// </summary>
        Available,

        /// <summary>
        /// The package has been loaded and is ready for use.
        /// </summary>
        Active,

        /// <summary>
        /// The package has been disabled. The use of the package is not possible.
        /// </summary>
        Disable
    }
}
