using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebTheme.Model
{
    /// <summary>
    /// Represents a dictionary that stores theme items based on plugin and application contexts.
    /// </summary>
    /// <remarks>
    /// The package scan adds and removes themes on its own thread while every page render reads
    /// them, and a dictionary written during a read can throw or loop forever. Every member
    /// therefore runs under one lock and returns a list taken under it.
    /// </remarks>
    internal class ThemeItemDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, List<ThemeItem>>> _dictionary = [];
        private readonly Lock _lock = new();

        /// <summary>
        /// Gets all theme items.
        /// </summary>
        public IEnumerable<ThemeItem> All
        {
            get
            {
                lock (_lock)
                {
                    return [.. _dictionary.Values.SelectMany(x => x.Values).SelectMany(x => x)];
                }
            }
        }

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

            lock (_lock)
            {
                if (!_dictionary.TryGetValue(pluginContext, out var appContextDict))
                {
                    appContextDict = [];
                    _dictionary[pluginContext] = appContextDict;
                }

                if (!appContextDict.TryGetValue(applicationContext, out var themeList))
                {
                    themeList = [];
                    appContextDict[applicationContext] = themeList;
                }

                themeList.RemoveAll(x => x.ThemeContext?.ThemeId == themeItem.ThemeContext.ThemeId);
                themeList.Add(themeItem);

                return true;
            }
        }

        /// <summary>
        /// Removes all themes associated with the specified plugin context and disposes them.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the resources to remove.</param>
        /// <returns>The theme contexts that were removed.</returns>
        public IEnumerable<IThemeContext> Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return [];
            }

            List<ThemeItem> items;

            lock (_lock)
            {
                // the plugin has not been registered in the manager
                if (!_dictionary.Remove(pluginContext, out var value))
                {
                    return [];
                }

                items = [.. value.Values.SelectMany(x => x)];
            }

            return Dispose(items);
        }

        /// <summary>
        /// Removes all themes associated with the specified application context and disposes them.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the resources to remove.</param>
        /// <returns>The theme contexts that were removed.</returns>
        internal IEnumerable<IThemeContext> Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return [];
            }

            var items = new List<ThemeItem>();

            lock (_lock)
            {
                foreach (var pluginDict in _dictionary.Values)
                {
                    if (pluginDict.Remove(applicationContext, out var themeList))
                    {
                        items.AddRange(themeList);
                    }
                }
            }

            return Dispose(items);
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check.</param>
        /// <returns>True if the plugin context exists in the dictionary, otherwise false.</returns>
        public bool ContainsPlugin(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dictionary.ContainsKey(pluginContext);
            }
        }

        /// <summary>
        /// Checks if the dictionary contains the specified application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check.</param>
        /// <param name="applicationContext">The application context to check.</param>
        /// <returns>True if the application context exists in the dictionary, otherwise false.</returns>
        public bool ContainsApplication(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && applicationContext is not null
                    && _dictionary.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext);
            }
        }

        /// <summary>
        /// Returns the theme items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of theme items</returns>
        public IEnumerable<ThemeItem> GetThemeItems(IApplicationContext applicationContext)
        {
            if (applicationContext?.PluginContext is null)
            {
                return [];
            }

            lock (_lock)
            {
                return _dictionary.TryGetValue(applicationContext.PluginContext, out var appContextDict)
                    && appContextDict.TryGetValue(applicationContext, out var themeList)
                    ? [.. themeList]
                    : [];
            }
        }

        /// <summary>
        /// Returns an enumeration of all containing theme contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose theme are to be registered.</param>
        /// <returns>An enumeration of theme contexts.</returns>
        public IEnumerable<IThemeContext> GetThemes(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return [];
            }

            lock (_lock)
            {
                return _dictionary.TryGetValue(pluginContext, out var pluginResources)
                    ? [.. pluginResources.SelectMany(x => x.Value).Select(x => x.ThemeContext)]
                    : [];
            }
        }

        /// <summary>
        /// Returns an enumeration of theme contextes.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of theme contextes.</returns>
        public IEnumerable<IThemeContext> GetThemes(IApplicationContext applicationContext)
        {
            lock (_lock)
            {
                return [.. _dictionary.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x)
                    .Where(x => x.ThemeContext.ApplicationContext.Equals(applicationContext))
                    .Select(x => x.ThemeContext)];
            }
        }

        /// <summary>
        /// Disposes removed theme items outside the lock, so their clean-up cannot block the
        /// page renders of other requests.
        /// </summary>
        /// <param name="items">The removed theme items.</param>
        /// <returns>Their theme contexts.</returns>
        private static List<IThemeContext> Dispose(List<ThemeItem> items)
        {
            foreach (var item in items)
            {
                item.Dispose();
            }

            return [.. items.Select(x => x.ThemeContext)];
        }
    }
}
