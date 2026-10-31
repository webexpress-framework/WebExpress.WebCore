using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebTheme.Model
{
    /// <summary>
    /// Represents a dictionary that stores theme items based on plugin and application contexts.
    /// </summary>
    internal class ThemeItemDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, List<ThemeItem>>> _dictionary = [];

        /// <summary>
        /// Gets all theme items.
        /// </summary>
        public IEnumerable<ThemeItem> All => _dictionary.Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x);

        /// <summary>
        /// Adds a theme item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="themeItem">The theme item.</param>
        /// <returns>True if the theme item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddThemeItem(IPluginContext pluginContext, IApplicationContext applicationContext, ThemeItem themeItem)
        {
            var type = themeItem.ThemeClass;

            if (!typeof(ITheme).IsAssignableFrom(type))
            {
                return false;
            }

            if (!_dictionary.ContainsKey(pluginContext))
            {
                _dictionary[pluginContext] = [];
            }

            var appContextDict = _dictionary[pluginContext];

            if (!appContextDict.TryGetValue(applicationContext, out List<ThemeItem> value))
            {
                value = [];
                appContextDict[applicationContext] = value;
            }

            var assetList = value;

            assetList.RemoveAll(x => x.ThemeContext?.ThemeId == themeItem.ThemeContext.ThemeId);
            assetList.Add(themeItem);

            return true;
        }

        /// <summary>
        /// Removes all resources associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the resources to remove.</param>
        /// <returns>An enumeration of theme contexts that were removed.</returns>
        public IEnumerable<IThemeContext> Remove(IPluginContext pluginContext)
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

                return items.Select(x => x.ThemeContext);
            }

            return [];
        }

        /// <summary>
        /// Removes all themes associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the resources to remove.</param>
        /// <returns>An enumeration of theme contexts that were removed.</returns>
        internal IEnumerable<IThemeContext> Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return [];
            }

            var removedThemes = new List<IThemeContext>();

            foreach (var pluginDict in _dictionary.Values)
            {
                foreach (var themeList in pluginDict.Where(x => x.Key == applicationContext).Select(x => x.Value))
                {
                    foreach (var assetItem in themeList)
                    {
                        removedThemes.Add(assetItem.ThemeContext);
                        assetItem.Dispose();
                    }
                }

                pluginDict.Remove(applicationContext);
            }

            return removedThemes;
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
        /// Returns the theme items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of theme items</returns>
        public IEnumerable<ThemeItem> GetThemeItems(IApplicationContext applicationContext)
        {
            if (_dictionary.ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = _dictionary[applicationContext?.PluginContext];

                if (appContextDict.TryGetValue(applicationContext, out List<ThemeItem> value))
                {
                    var assetList = value;

                    return assetList;
                }
            }

            return [];
        }

        /// <summary>
        /// Returns an enumeration of all containing theme contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose theme are to be registered.</param>
        /// <returns>An enumeration of theme contexts.</returns>
        public IEnumerable<IThemeContext> GetThemes(IPluginContext pluginContext)
        {
            if (_dictionary.TryGetValue(pluginContext, out var pluginResources))
            {
                return pluginResources
                    .SelectMany(x => x.Value)
                    .Select(x => x.ThemeContext);
            }

            return [];
        }

        /// <summary>
        /// Returns an enumeration of theme contextes.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of theme contextes.</returns>
        public IEnumerable<IThemeContext> GetThemes(IApplicationContext applicationContext)
        {
            return _dictionary.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x)
                .Where(x => x.ThemeContext.ApplicationContext.Equals(applicationContext))
                .Select(x => x.ThemeContext);
        }
    }
}
