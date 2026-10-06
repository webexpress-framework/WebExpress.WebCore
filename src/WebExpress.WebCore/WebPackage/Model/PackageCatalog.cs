using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace WebExpress.WebCore.WebPackage.Model
{
    /// <summary>
    /// The list of packages known to WebExpress, serialized to and from an XML catalog file. Each
    /// entry (<see cref="PackageCatalogItem"/>) describes one installable package and its state.
    /// </summary>
    [XmlRoot("catalog")]
    public class PackageCatalog
    {
        /// <summary>
        /// Gets the package entries in the catalog.
        /// </summary>
        /// <remarks>
        /// This is the persisted collection and holds installed packages only. The plugins that
        /// ship with the application are not packages and never enter it; they are synthesized on
        /// read by <see cref="IPackageManager.GetPackages"/> and marked
        /// <see cref="PackageCatalogItem.BuiltIn"/>.
        /// </remarks>
        [XmlElement("package")]
        public List<PackageCatalogItem> Packages { get; } = [];

        /// <summary>
        /// Locates a specific catalog item.
        /// </summary>
        /// <param name="id">The package id.</param>
        /// <returns>The catalog item or null.</returns>
        public PackageCatalogItem Find(string id)
        {
            return Packages
                .Where(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }
    }
}
