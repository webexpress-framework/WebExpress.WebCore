using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
        /// A built-in entry stands for a plugin the <see cref="WebPlugin.IPluginManager"/>
        /// registered from the application directory, so the management surface can list it next
        /// to the installed packages. It has no package file behind it and is never written to the
        /// catalog as a package. It can be deactivated and activated - its plugin is withdrawn and
        /// registered again from the loaded assembly - but neither updated nor uninstalled: an
        /// assembly in the application directory cannot be replaced or unloaded at runtime.
        /// </remarks>
        [XmlIgnore]
        public bool BuiltIn { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the entry stands for a system plugin - a part of the
        /// framework such as webexpress.webui - which cannot be deactivated at all.
        /// </summary>
        /// <remarks>
        /// The applications and the management surface itself are built on the system plugins;
        /// withdrawing one would take everything down, including the way to switch it on again.
        /// </remarks>
        [XmlIgnore]
        public bool SystemPlugin { get; internal set; }

        /// <summary>
        /// Gets or sets the assembly a built-in plugin is registered from again when it is activated.
        /// </summary>
        [XmlIgnore]
        internal Assembly Assembly { get; set; }

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
        /// Gets why a package that is neither disabled nor running is not running - the
        /// dependencies it waits for or the error its start ended with - or null when there is
        /// nothing to report.
        /// </summary>
        /// <remarks>
        /// The state alone cannot tell an administrator whether waiting is enough or whether the
        /// package needs attention; this text is what the management surface shows for it. It is
        /// in the server's language; a surface translates it for its reader with
        /// <see cref="GetProblem(CultureInfo)"/>.
        /// </remarks>
        [XmlIgnore]
        public string Problem => ProblemText?.ToString();

        /// <summary>
        /// Gets or sets the reason behind <see cref="Problem"/>, kept untranslated because it is
        /// recorded by a scan, long before a request in some language shows it.
        /// </summary>
        [XmlIgnore]
        internal PackageText ProblemText { get; set; }

        /// <summary>
        /// Gets a value indicating whether the last start of the package failed. A faulted
        /// package is not retried by the scan - it would fail again every round - but only when
        /// its file changes or an administrator activates it.
        /// </summary>
        [XmlIgnore]
        internal bool Faulted { get; set; }

        /// <summary>
        /// Gets the directory the running version of the package was extracted to, or null while
        /// it is not loaded.
        /// </summary>
        [XmlIgnore]
        internal string ExtractedPath { get; set; }

        /// <summary>
        /// Returns why the package is not running in the language of a culture.
        /// </summary>
        /// <param name="culture">The culture of the reader, or null for the server's default culture.</param>
        /// <returns>The reason, or null when there is nothing to report.</returns>
        public string GetProblem(CultureInfo culture)
        {
            return ProblemText?.Translate(culture);
        }

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
