using System.Collections.Generic;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebComponent
{
    /// <summary>
    /// A component manager that fills itself from plugins: when a plugin is loaded it discovers and
    /// registers the relevant entries, and when a plugin is unloaded it removes them again. Most of
    /// the framework's managers implement this so their content follows the set of active plugins.
    /// </summary>
    public interface IComponentManagerPlugin : IComponentManager
    {
        /// <summary>
        /// Discovers and registers entries from the specified plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose elements are to be registered.</param>
        void Register(IPluginContext pluginContext);

        /// <summary>
        /// Discovers and registers entries from the specified plugin.
        /// </summary>
        /// <param name="pluginContexts">A list with plugin contexts that contain the elements.</param>
        void Register(IEnumerable<IPluginContext> pluginContexts);

        /// <summary>
        /// Removes all elemets associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the elemets to remove.</param>
        void Remove(IPluginContext pluginContext);
    }
}
