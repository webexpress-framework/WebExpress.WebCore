using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebPage.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application contexts, page types, and page items.
    /// key = plugin context
    /// value = application context { key = page type, value = page item }
    /// </summary>
    /// <remarks>
    /// The package scan adds and removes pages on its own thread while requests look them up, and
    /// a dictionary written during a read can throw or loop forever. Every member therefore runs
    /// under one lock and returns a list taken under it, never a query that would read the
    /// dictionary later, outside the lock.
    /// </remarks>
    internal class PageDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, PageItem>>> _dict = [];
        private readonly Lock _lock = new();

        /// <summary>
        /// Gets all page contexts.
        /// </summary>
        public IEnumerable<IPageContext> All
        {
            get
            {
                lock (_lock)
                {
                    return [.. Items().Select(x => x.PageContext)];
                }
            }
        }

        /// <summary>
        /// Adds a page item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="pageItem">The page item.</param>
        /// <returns>True if the page item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddPageItem(IPluginContext pluginContext, IApplicationContext applicationContext, PageItem pageItem)
        {
            var type = pageItem.PageClass;

            if (type.GetInterface(typeof(IPage<>).Name) is null)
            {
                return false;
            }

            lock (_lock)
            {
                if (!_dict.TryGetValue(pluginContext, out var appContextDict))
                {
                    appContextDict = [];
                    _dict[pluginContext] = appContextDict;
                }

                if (!appContextDict.TryGetValue(applicationContext, out var pageDict))
                {
                    pageDict = [];
                    appContextDict[applicationContext] = pageDict;
                }

                return pageDict.TryAdd(type, pageItem);
            }
        }

        /// <summary>
        /// Removes all pages of a plugin and disposes them.
        /// </summary>
        /// <remarks>
        /// The removed items are collected before they leave the dictionary; a query read after the
        /// removal found nothing, so no page was disposed and no remove event fired, which left the
        /// endpoints of a removed plugin to anyone listening for the removal.
        /// </remarks>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The page contexts that were removed.</returns>
        public IReadOnlyList<IPageContext> RemovePage(IPluginContext pluginContext)
        {
            List<PageItem> removed;

            lock (_lock)
            {
                if (pluginContext is null || !_dict.Remove(pluginContext, out var appContextDict))
                {
                    return [];
                }

                removed = [.. appContextDict.Values.SelectMany(x => x.Values)];
            }

            return Dispose(removed);
        }

        /// <summary>
        /// Removes all pages of an application and disposes them.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>The page contexts that were removed.</returns>
        public IReadOnlyList<IPageContext> RemovePage(IApplicationContext applicationContext)
        {
            var removed = new List<PageItem>();

            lock (_lock)
            {
                foreach (var (pluginContext, appContextDict) in _dict.ToList())
                {
                    if (!appContextDict.Remove(applicationContext, out var pageDict))
                    {
                        continue;
                    }

                    if (appContextDict.Count == 0)
                    {
                        _dict.Remove(pluginContext);
                    }

                    removed.AddRange(pageDict.Values);
                }
            }

            return Dispose(removed);
        }

        /// <summary>
        /// Returns the page items associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The page items.</returns>
        public IEnumerable<PageItem> GetPageItems(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dict.TryGetValue(pluginContext, out var appContextDict)
                    ? [.. appContextDict.Values.SelectMany(x => x.Values)]
                    : [];
            }
        }

        /// <summary>
        /// Returns the page items associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>The page items.</returns>
        public IEnumerable<PageItem> GetPageItems(IApplicationContext applicationContext)
        {
            lock (_lock)
            {
                return [.. _dict.Values
                    .SelectMany(x => x)
                    .Where(x => x.Key.Equals(applicationContext))
                    .SelectMany(x => x.Value.Values)];
            }
        }

        /// <summary>
        /// Returns the page item associated with the specified page context.
        /// </summary>
        /// <param name="pageContext">The page context.</param>
        /// <returns>The page item, or null.</returns>
        public PageItem GetPageItem(IPageContext pageContext)
        {
            lock (_lock)
            {
                return Items().FirstOrDefault(x => x.PageContext.Equals(pageContext));
            }
        }

        /// <summary>
        /// Returns the page items from the dictionary for a specific application context and page type.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="pageType">The page type.</param>
        /// <returns>The page items.</returns>
        public IEnumerable<PageItem> GetPageItems(IApplicationContext applicationContext, Type pageType)
        {
            if (!typeof(IPage).IsAssignableFrom(pageType) || applicationContext?.PluginContext is null)
            {
                return [];
            }

            lock (_lock)
            {
                return _dict.TryGetValue(applicationContext.PluginContext, out var appContextDict)
                    && appContextDict.TryGetValue(applicationContext, out var pageDict)
                    && pageDict.TryGetValue(pageType, out var value)
                    ? [value]
                    : [];
            }
        }

        /// <summary>
        /// Returns an enumeration of all containing page contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The page contexts.</returns>
        public IEnumerable<IPageContext> GetPages(IPluginContext pluginContext)
        {
            return [.. GetPageItems(pluginContext).Select(x => x.PageContext)];
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <param name="pageType">The page type.</param>
        /// <returns>The page contexts.</returns>
        public IEnumerable<IPageContext> GetPages(Type pageType)
        {
            lock (_lock)
            {
                return [.. Items()
                    .Where(x => x.PageClass.Equals(pageType))
                    .Select(x => x.PageContext)];
            }
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <param name="pageType">The page type.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>The page contexts.</returns>
        public IEnumerable<IPageContext> GetPages(Type pageType, IApplicationContext applicationContext)
        {
            lock (_lock)
            {
                return [.. Items()
                    .Where(x => x.PageClass.Equals(pageType))
                    .Where(x => x.PageContext.ApplicationContext.Equals(applicationContext))
                    .Select(x => x.PageContext)];
            }
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <typeparam name="TPage">The page type.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>The page contexts.</returns>
        public IEnumerable<IPageContext> GetPages<TPage>(IApplicationContext applicationContext) where TPage : IPage
        {
            return GetPages(typeof(TPage), applicationContext);
        }

        /// <summary>
        /// Returns the page context.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="pageId">The page id.</param>
        /// <returns>The page context, or null.</returns>
        public IPageContext GetPage(IApplicationContext applicationContext, string pageId)
        {
            lock (_lock)
            {
                return Items()
                    .Where(x => x.PageContext.ApplicationContext.Equals(applicationContext))
                    .Where(x => x.PageContext.EndpointId.Equals(pageId))
                    .Select(x => x.PageContext)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Returns the page context.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <param name="pageId">The page id.</param>
        /// <returns>The page context, or null.</returns>
        public IPageContext GetPage(string applicationId, string pageId)
        {
            lock (_lock)
            {
                return Items()
                    .Where(x => x.PageContext.ApplicationContext.ApplicationId.Equals(applicationId))
                    .Where(x => x.PageContext.EndpointId.Equals(pageId))
                    .Select(x => x.PageContext)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <returns>True if the plugin context exists in the dictionary, otherwise false.</returns>
        public bool Contains(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dict.ContainsKey(pluginContext);
            }
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <param name="applicationContext">The application context to check for.</param>
        /// <returns>True if the plugin context and application context exist in the dictionary, otherwise false.</returns>
        public bool Contains(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && applicationContext is not null
                    && _dict.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext);
            }
        }

        /// <summary>
        /// Enumerates all page items; called under the lock only.
        /// </summary>
        /// <returns>The page items.</returns>
        private IEnumerable<PageItem> Items()
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values);
        }

        /// <summary>
        /// Disposes removed page items outside the lock, so a page's own clean-up cannot block the
        /// lookups of other requests.
        /// </summary>
        /// <param name="removed">The removed page items.</param>
        /// <returns>Their page contexts.</returns>
        private static IReadOnlyList<IPageContext> Dispose(List<PageItem> removed)
        {
            foreach (var item in removed)
            {
                item.Dispose();
            }

            return [.. removed.Select(x => x.PageContext)];
        }
    }
}
