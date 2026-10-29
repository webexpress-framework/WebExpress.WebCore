using System.Collections.Generic;

namespace WebExpress.WebCore.WebPackage.Model
{
    /// <summary>
    /// Represents an item in a web package.
    /// </summary>
    public class PackageItem
    {
        /// <summary>
        /// Gets or sets the package file name.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets Returns or sets the id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the titles.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the authors.
        /// </summary>
        public string Authors { get; set; }

        /// <summary>
        /// Gets or sets the license.
        /// </summary>
        public string License { get; set; }

        /// <summary>
        /// Gets or sets the package icon.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Gets or sets the readme file of the package (md format).
        /// </summary>
        public string Readme { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the tags.
        /// </summary>
        public string Tags { get; set; }

        /// <summary>
        /// Gets or sets the plugin sources.
        /// </summary>
        public IEnumerable<string> PluginSources { get; set; }

        /// <summary>
        /// Gets or sets the package dependencies.
        /// </summary>
        public IEnumerable<string> Dependencies { get; set; }

        /// <summary>
        /// Gets or sets the names of the settings files the package ships, as they are deployed
        /// to the settings directory of the server.
        /// </summary>
        public IEnumerable<string> Settings { get; set; } = [];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        internal PackageItem()
        {
        }

        /// <summary>
        /// Convert the package element to a string.
        /// </summary>
        /// <returns>The package element in its string representation.</returns>
        public override string ToString()
        {
            return $"Package '{Id}'";
        }
    }
}
