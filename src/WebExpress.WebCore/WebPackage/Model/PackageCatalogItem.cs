using System.Collections.Generic;
using System.Xml.Serialization;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebPackage.Model
{
    /// <summary>
    /// One package entry in the <see cref="PackageCatalog"/>. It records a package's identity and
    /// metadata together with its current state (available, active, or disabled).
    /// </summary>
    [XmlRoot("package")]
    public class PackageCatalogItem
    {
        /// <summary>
        /// Gets or sets Returns or sets the id.
        /// </summary>
        [XmlAttribute("id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the filename.
        /// </summary>
        [XmlAttribute("file")]
        public string File { get; set; }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        [XmlAttribute("state")]
        public PackageCatalogItemState State { get; set; }

        /// <summary>
        /// Gets a value indicating whether the entry stands for a plugin that ships with the
        /// application rather than for an installed package.
        /// </summary>
        /// <remarks>
        /// A built-in entry is synthesized on read from the plugins the
        /// <see cref="WebPlugin.IPluginManager"/> registered from the application directory, so
        /// the management surface can list them next to the installed packages. It has no package
        /// file behind it, is never written to the catalog, and none of the package operations
        /// apply to it - an assembly in the application directory cannot be uninstalled at runtime.
        /// </remarks>
        [XmlIgnore]
        public bool BuiltIn { get; internal set; }

        /// <summary>
        /// Gets the plugins belonging to the package.
        /// </summary>
        [XmlIgnore]
        public List<IPluginContext> Plugins { get; internal set; } = [];

        /// <summary>
        /// Gets or sets the meta information about the package.
        /// </summary>
        [XmlIgnore]
        public PackageItem Metadata { get; set; }

        /// <summary>
        /// Conversion into a string representation of the object.
        /// </summary>
        /// <returns>The object as a string.</returns>
        public override string ToString()
        {
            return Id;
        }
    }
}
