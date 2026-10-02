using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebAsset.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application contexts and asset items.
    /// key = plugin context
    /// value = { key = resource type, value = ressource item }
    /// </summary>
    internal class AssetItemDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, List<AssetItem>>> _dictionary = [];

        /// <summary>
        /// Gets all asset items.
        /// </summary>
        public IEnumerable<AssetItem> All => _dictionary.Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x);

        /// <summary>
        /// Returns the asset item that matches the given asset context (by endpoint id).
        /// Callers must synchronize access to this collection.
        /// </summary>
        /// <param name="assetContext">The asset context resolved by the sitemap.</param>
        /// <returns>The matching asset item, or null when none matches.</returns>
        public AssetItem GetByContext(IAssetContext assetContext)
        {
            if (assetContext is null)
            {
                return null;
            }

            return _dictionary.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x)
                .FirstOrDefault(x => x.AssetContext?.EndpointId?.Equals(assetContext.EndpointId) ?? false);
        }

        /// <summary>
        /// Returns the asset item whose route matches the end of the given request uri.
        /// This is a fallback for the rare case that the resolved endpoint context is
        /// not an asset context. Callers must synchronize access to this collection.
        /// </summary>
        /// <param name="requestUri">The request uri.</param>
        /// <returns>The matching asset item, or null when none matches.</returns>
        public AssetItem FindByRoute(string requestUri)
        {
            if (string.IsNullOrEmpty(requestUri))
            {
                return null;
            }

            var normalized = requestUri.ToLower().Replace("/", ".");

            return _dictionary.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x)
                .FirstOrDefault(x => normalized.EndsWith(x.AssetContext.Route.ToString().Replace("/", ".")));
        }

        /// <summary>
        /// Adds an asset item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="assetItem">The resource item.</param>
        /// <returns>True if the resource item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddAssetItem(IPluginContext pluginContext, IApplicationContext applicationContext, AssetItem assetItem)
        {
            var type = assetItem.AssetClass;

            if (!typeof(IAsset).IsAssignableFrom(type))
            {
                return false;
            }

            if (!_dictionary.ContainsKey(pluginContext))
            {
                _dictionary[pluginContext] = [];
            }

            var appContextDict = _dictionary[pluginContext];

            if (!appContextDict.TryGetValue(applicationContext, out List<AssetItem> value))
            {
                value = ([]);
                appContextDict[applicationContext] = value;
            }

            var assetList = value;

            assetList.RemoveAll(x => x.AssetContext?.EndpointId?.Equals(assetItem.AssetContext.EndpointId) ?? false);
            assetList.Add(assetItem);

            return true;
        }

        /// <summary>
        /// Removes all resources associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the resources to remove.</param>
        /// <returns>An enumeration of asset contexts that were removed.</returns>
        public IEnumerable<IAssetContext> Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return [];
            }

            // the plugin has not been registered in the manager
            if (_dictionary.TryGetValue(pluginContext, out var value))
            {
                var items = value.Values
                    .SelectMany(x => x)
                    .ToList();

                foreach (var assetItem in items)
                {
                    assetItem.Dispose();
                }

                _dictionary.Remove(pluginContext);

                return items.Select(x => x.AssetContext);
            }

            return [];
        }

        /// <summary>
        /// Removes all assets associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the resources to remove.</param>
        /// <returns>An enumeration of asset contexts that were removed.</returns>
        internal IEnumerable<IAssetContext> Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return [];
            }

            var removedAssets = new List<IAssetContext>();

            foreach (var pluginDict in _dictionary.Values)
            {
                foreach (var assetList in pluginDict.Where(x => x.Key == applicationContext).Select(x => x.Value))
                {
                    foreach (var assetItem in assetList)
                    {
                        removedAssets.Add(assetItem.AssetContext);
                        assetItem.Dispose();
                    }
                }

                pluginDict.Remove(applicationContext);
            }

            return removedAssets;
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check.</param>
        /// <returns>True if the plugin context exists in the dictionary, otherwise false.</returns>
        public bool ContainsPlugin(IPluginContext pluginContext)
        {
            return _dictionary.ContainsKey(pluginContext);
        }

        /// <summary>
        /// Checks if the dictionary contains the specified application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check.</param>
        /// <param name="applicationContext">The application context to check.</param>
        /// <returns>True if the application context exists in the dictionary, otherwise false.</returns>
        public bool ContainsApplication(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            if (_dictionary.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the asset items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of asset items</returns>
        public IEnumerable<AssetItem> GetAssetItems(IApplicationContext applicationContext)
        {
            if (_dictionary.ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = _dictionary[applicationContext?.PluginContext];

                if (appContextDict.TryGetValue(applicationContext, out List<AssetItem> value))
                {
                    var assetList = value;

                    return assetList;
                }
            }

            return [];
        }

        /// <summary>
        /// Returns an enumeration of all containing asset contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose asset are to be registered.</param>
        /// <returns>An enumeration of asset contexts.</returns>
        public IEnumerable<IAssetContext> GetAssets(IPluginContext pluginContext)
        {
            if (_dictionary.TryGetValue(pluginContext, out var pluginResources))
            {
                return pluginResources
                    .SelectMany(x => x.Value)
                    .Select(x => x.AssetContext);
            }

            return [];
        }

        /// <summary>
        /// Returns an enumeration of asset contextes.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of asset contextes.</returns>
        public IEnumerable<IAssetContext> GetAssets(IApplicationContext applicationContext)
        {
            return _dictionary.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x)
                .Where(x => x.AssetContext.ApplicationContext.Equals(applicationContext))
                .Select(x => x.AssetContext);
        }
    }
}
