using System.Reflection;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Represents the context of a plugin, providing access to its metadata and host context.
    /// </summary>
    public class PluginContext : IPluginContext
    {
        /// <summary>
        /// Gets the assembly that contains the plugin.
        /// </summary>
        public Assembly Assembly { get; internal set; }

        /// <summary>
        /// Gets the plugin id.
        /// </summary>
        public IComponentId PluginId { get; internal set; }

        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        public string PluginName { get; internal set; }

        /// <summary>
        /// Gets the manufacturer of the plugin.
        /// </summary>
        public string Manufacturer { get; internal set; }

        /// <summary>
        /// Gets the copyright information.
        /// </summary>
        public string Copyright { get; internal set; }

        /// <summary>
        /// Gets the description of the plugin.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        public string Version { get; internal set; }

        /// <summary>
        /// Gets the license information.
        /// </summary>
        public string License { get; internal set; }

        /// <summary>
        /// Gets the icon of the plugin.
        /// </summary>
        public IRoute Icon { get; internal set; }

        /// <summary>
        /// Gets the settings of the plugin.
        /// </summary>
        public IConfiguration Settings { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public PluginContext()
        {
        }

        /// <summary>
        /// Conversion of the plugin context into its string representation.
        /// </summary>
        /// <returns>The string that uniquely represents the plugin.</returns>
        public override string ToString()
        {
            return $"Plugin: {PluginId}";
        }
    }
}
