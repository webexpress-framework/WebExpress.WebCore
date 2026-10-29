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
        /// ship with the application are not packages and never enter it; they are listed by
        /// <see cref="IPackageManager.GetPackages"/> and marked
        /// <see cref="PackageCatalogItem.BuiltIn"/>.
        /// </remarks>
        [XmlElement("package")]
        public List<PackageCatalogItem> Packages { get; } = [];

        /// <summary>
        /// Gets the ids of the plugins shipping with the application that an administrator
        /// deactivated.
        /// </summary>
        /// <remarks>
        /// Such a plugin has no package entry to carry its state, yet the decision has to outlive
        /// the process: a plugin listed here is withdrawn before it boots on the next start.
        /// </remarks>
        [XmlElement("disabled")]
        public List<string> DisabledPlugins { get; } = [];

        /// <summary>
        /// Gets the assembly file names of the plugins shipping with the application that an
        /// administrator uninstalled.
        /// </summary>
        /// <remarks>
        /// The assembly of such a plugin is loaded into the default context and, on windows,
        /// locked until the process ends, so it is deleted on the next start, before the plugins
        /// are loaded. A file listed in the host's dependency manifest stays - the host would not
        /// start without it - and is left out of the loading on every start instead.
        /// </remarks>
        [XmlElement("removed")]
        public List<string> RemovedPlugins { get; } = [];

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
