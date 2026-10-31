using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin.Model;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Contract for the plugin registry. It tracks which plugins are loaded, raises events when a
    /// plugin is added or removed, and gives the rest of the framework access to the active plugins.
    /// </summary>
    public interface IPluginManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an plugin is added.
        /// </summary>
        event EventHandler<IPluginContext> AddPlugin;

        /// <summary>
        /// An event that fires when an plugin is removed.
        /// </summary>
        event EventHandler<IPluginContext> RemovePlugin;

        /// <summary>
        /// Gets all plugins.
        /// </summary>
        IEnumerable<IPluginContext> Plugins { get; }

        /// <summary>
        /// Returns a plugin context based on its id.
        /// </summary>
        /// <param name="pluginId">The id of the plugin.</param>
        /// <returns>The plugin context.</returns>
        IPluginContext GetPlugin(string pluginId);

        /// <summary>
        /// Returns a plugin context based on its id.
        /// </summary>
        /// <param name="plugin">The type of the plugin.</param>
        /// <returns>The plugin context.</returns>
        IPluginContext GetPlugin(Type plugin);

        /// <summary>
        /// Returns all plugins that have associated applications.
        /// </summary>
        /// <param name="applicationContext">The application context to filter plugins.</param>
        /// <returns>An enumerable collection of plugin contexts with applications.</returns>
        IEnumerable<IPluginContext> GetPlugins(IApplicationContext applicationContext);

        /// <summary>
        /// Returns all ApplicationContext instances associated with a plugin.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin.</param>
        /// <returns>A collection of ApplicationContext instances.</returns>
        IEnumerable<IApplicationContext> GetAssociatedApplications(IPluginContext pluginContext);

        /// <summary>
        /// Gets runtime metadata for all known plugins, including dependency and status information.
        /// </summary>
        /// <returns>A list of plugin runtime metadata entries.</returns>
        IEnumerable<PluginRuntimeInfo> GetPluginRuntimeInfos();
    }
}
