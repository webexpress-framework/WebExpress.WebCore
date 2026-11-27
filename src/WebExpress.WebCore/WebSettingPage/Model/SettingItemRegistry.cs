using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebSettingPage.Model
{
    /// <summary>
    /// Holds the items of the setting pages - categories, groups or pages - by plugin and
    /// application.
    /// </summary>
    /// <remarks>
    /// The package scan adds and removes them on its own thread while requests build the setting
    /// navigation, and a dictionary written during a read can throw or loop forever. Every member
    /// therefore runs under one lock and returns a list taken under it; removing returns the
    /// removed items, so the caller can announce them.
    /// </remarks>
    /// <typeparam name="TItem">The type of the items.</typeparam>
    internal abstract class SettingItemRegistry<TItem>
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, List<TItem>>> _dict = [];
        private readonly Lock _lock = new();

        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="item">The item.</param>
        /// <returns>Always true.</returns>
        protected bool Add(IPluginContext pluginContext, IApplicationContext applicationContext, TItem item)
        {
            lock (_lock)
            {
                if (!_dict.TryGetValue(pluginContext, out var appDict))
                {
                    appDict = [];
                    _dict.Add(pluginContext, appDict);
                }

                if (!appDict.TryGetValue(applicationContext, out var list))
                {
                    list = [];
                    appDict.Add(applicationContext, list);
                }

                list.Add(item);

                return true;
            }
        }

        /// <summary>
        /// Removes all items of a plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The removed items.</returns>
        public IReadOnlyList<TItem> Remove(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dict.Remove(pluginContext, out var appDict)
                    ? [.. appDict.Values.SelectMany(x => x)]
                    : [];
            }
        }

        /// <summary>
        /// Removes all items of an application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>The removed items.</returns>
        public IReadOnlyList<TItem> Remove(IApplicationContext applicationContext)
        {
            var removed = new List<TItem>();

            if (applicationContext is null)
            {
                return removed;
            }

            lock (_lock)
            {
                foreach (var (pluginContext, appDict) in _dict.ToList())
                {
                    if (appDict.Remove(applicationContext, out var list))
                    {
                        removed.AddRange(list);
                    }

                    if (appDict.Count == 0)
                    {
                        _dict.Remove(pluginContext);
                    }
                }
            }

            return removed;
        }

        /// <summary>
        /// Determines whether items of the plugin are registered.
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
        /// Determines whether items of the application are registered.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>True if the application is known.</returns>
        public bool Contains(IApplicationContext applicationContext)
        {
            lock (_lock)
            {
                return applicationContext is not null && _dict.Values.Any(x => x.ContainsKey(applicationContext));
            }
        }

        /// <summary>
        /// Returns a copy of the items, optionally of one application only.
        /// </summary>
        /// <param name="applicationContext">The application context, or null for all.</param>
        /// <returns>The items.</returns>
        protected List<TItem> Items(IApplicationContext applicationContext = null)
        {
            lock (_lock)
            {
                return [.. _dict.Values
                    .SelectMany(x => x)
                    .Where(x => applicationContext is null || x.Key == applicationContext)
                    .SelectMany(x => x.Value)];
            }
        }

        /// <summary>
        /// Runs an operation on one item under the lock, for state the item carries itself.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="predicate">Selects the item.</param>
        /// <param name="operation">The operation, called with the item or its default.</param>
        /// <returns>The result of the operation.</returns>
        protected TResult WithItem<TResult>(Func<TItem, bool> predicate, Func<TItem, TResult> operation)
        {
            lock (_lock)
            {
                return operation(_dict.Values.SelectMany(x => x.Values).SelectMany(x => x).FirstOrDefault(predicate));
            }
        }
    }
}
