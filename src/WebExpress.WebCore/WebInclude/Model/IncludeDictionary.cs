using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebInclude.Model
{
    /// <summary>
    /// Represents the includes by plugin and application.
    /// Plugin -> Application -> include id -> IncludeItem
    /// </summary>
    /// <remarks>
    /// The package scan adds and removes includes on its own thread while every page render
    /// reads them, and a dictionary written during a read can throw or loop forever. Every member
    /// therefore runs under one lock and returns a list taken under it.
    /// </remarks>
    internal sealed class IncludeDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<string, IncludeItem>>> _dict = [];
        private readonly Lock _lock = new();

        /// <summary>
        /// Gets all include items.
        /// </summary>
        public IReadOnlyList<IncludeItem> All
        {
            get
            {
                lock (_lock)
                {
                    return [.. _dict.Values.SelectMany(x => x.Values).SelectMany(x => x.Values)];
                }
            }
        }

        /// <summary>
        /// Adds an include item.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="includeItem">The include item.</param>
        /// <returns>True if the item was added, false if it was invalid or already present.</returns>
        public bool AddIncludeItem(IPluginContext pluginContext, IApplicationContext applicationContext, IncludeItem includeItem)
        {
            if (pluginContext is null || applicationContext is null || includeItem is null)
            {
                return false;
            }

            var key = includeItem.IncludeId?.ToString() ?? includeItem.IncludeClass?.FullName?.ToLowerInvariant() ?? Guid.NewGuid().ToString("N");

            lock (_lock)
            {
                if (!_dict.TryGetValue(pluginContext, out var appDict))
                {
                    appDict = [];
                    _dict.Add(pluginContext, appDict);
                }

                if (!appDict.TryGetValue(applicationContext, out var includeMap))
                {
                    includeMap = new Dictionary<string, IncludeItem>(StringComparer.OrdinalIgnoreCase);
                    appDict.Add(applicationContext, includeMap);
                }

                return includeMap.TryAdd(key, includeItem);
            }
        }

        /// <summary>
        /// Checks whether includes of the plugin are registered.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>True if the plugin is known.</returns>
        public bool Contains(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dict.ContainsKey(pluginContext);
            }
        }

        /// <summary>
        /// Checks whether includes of the plugin are registered for the application.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>True if the pair is known.</returns>
        public bool Contains(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && applicationContext is not null
                    && _dict.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext);
            }
        }

        /// <summary>
        /// Removes the includes of a plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The removed items.</returns>
        public IReadOnlyList<IncludeItem> Remove(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dict.Remove(pluginContext, out var appDict)
                    ? [.. appDict.Values.SelectMany(x => x.Values)]
                    : [];
            }
        }

        /// <summary>
        /// Removes the includes of an application, together with a plugin entry left empty.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>The removed items.</returns>
        public IReadOnlyList<IncludeItem> Remove(IApplicationContext applicationContext)
        {
            var removed = new List<IncludeItem>();

            lock (_lock)
            {
                foreach (var (pluginContext, appDict) in _dict.ToList())
                {
                    if (!appDict.Remove(applicationContext, out var includeMap))
                    {
                        continue;
                    }

                    if (appDict.Count == 0)
                    {
                        _dict.Remove(pluginContext);
                    }

                    removed.AddRange(includeMap.Values);
                }
            }

            return removed;
        }

        /// <summary>
        /// Returns the include items of an application, optionally of one include class.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="includeType">The include class, or null for all.</param>
        /// <returns>The include items.</returns>
        public IReadOnlyList<IncludeItem> GetItems(IApplicationContext applicationContext, Type includeType = null)
        {
            lock (_lock)
            {
                return [.. _dict.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x.Values)
                    .Where(x => includeType is null || x.IncludeClass.Equals(includeType))
                    .Where(x => x.IncludeContext.ApplicationContext.Equals(applicationContext))];
            }
        }
    }
}
