using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// The asset manager manages resources elements, which can be called with a URI (Uniform Resource Identifier).
    /// </summary>
    public interface IAssetManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an asset is added.
        /// </summary>
        event EventHandler<IAssetContext> AddAsset;

        /// <summary>
        /// An event that fires when an asset is removed.
        /// </summary>
        event EventHandler<IAssetContext> RemoveAsset;

        /// <summary>
        /// Gets all asset contexts.
        /// </summary>
        IEnumerable<IAssetContext> Assets { get; }

        /// <summary>
        /// Returns an enumeration of all containing asset contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose resources are to be registered.</param>
        /// <returns>An enumeration of resource contexts.</returns>
        IEnumerable<IAssetContext> GetAssets(IPluginContext pluginContext);

        /// <summary>
        /// Returns an enumeration of asset contextes.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of asset contextes.</returns>
        IEnumerable<IAssetContext> GetAssets(IApplicationContext applicationContext);

        /// <summary>
        /// Returns the route the given embedded file of a plugin is served from within an
        /// application. Consumers that have to link an embedded file - an include rendering a
        /// link or script element - resolve it here instead of composing the route themselves,
        /// because a route composed independently drifts from the mount without anything
        /// noticing: the browser answers the resulting 404 with the html error page and accepts
        /// it as a stylesheet with no rules.
        /// </summary>
        /// <param name="applicationContext">The context of the application the file is addressed in.</param>
        /// <param name="pluginContext">The context of the plugin the file belongs to.</param>
        /// <param name="file">The file path relative to the plugin's mount, as declared on the asset attribute.</param>
        /// <returns>The route of the file, or null when the arguments do not describe one.</returns>
        IRoute GetAssetRoute(IApplicationContext applicationContext, IPluginContext pluginContext, string file);
    }
}
