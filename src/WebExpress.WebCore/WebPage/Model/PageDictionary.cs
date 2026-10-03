using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebPage.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application contexts, page types, and page items.
    /// key = plugin context
    /// value = application context { key = page type, value = page item }
    /// </summary>
    internal class PageDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, PageItem>>> _dict = [];

        /// <summary>
        /// Gets all page contexts.
        /// </summary>
        public IEnumerable<IPageContext> All => _dict.Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x.Values)
            .Select(x => x.PageContext);

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

            if (!_dict.TryGetValue(pluginContext, out Dictionary<IApplicationContext, Dictionary<Type, PageItem>> appContextDict))
            {
                appContextDict = ([]);
                _dict[pluginContext] = appContextDict;
            }

            if (!appContextDict.TryGetValue(applicationContext, out Dictionary<Type, PageItem> pageDict))
            {
                pageDict = ([]);
                appContextDict[applicationContext] = pageDict;
            }

            if (!pageDict.ContainsKey(type))
            {
                pageDict[type] = pageItem;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes all page from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        public IEnumerable<IPageContext> RemovePage(IPluginContext pluginContext)
        {
            var removed = GetPageItems(pluginContext);

            _dict.Remove(pluginContext);

            foreach (var item in removed)
            {
                item.Dispose();
            }

            return removed.Select(x => x.PageContext);
        }

        /// <summary>
        /// Removes all page from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        public IEnumerable<IPageContext> RemovePage(IApplicationContext applicationContext)
        {
            var removed = GetPageItems(applicationContext);

            foreach (var applicationDict in _dict.Values)
            {
                applicationDict.Remove(applicationContext);
            }

            foreach (var item in removed)
            {
                item.Dispose();
            }

            return removed.Select(x => x.PageContext);
        }

        /// <summary>
        /// Returns the page items associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to retrieve page items for.</param>
        /// <returns>An IEnumerable of <see cref="PageItem"/> associated with the specified application context.</returns>
        public IEnumerable<PageItem> GetPageItems(IPluginContext pluginContext)
        {
            return _dict.Where(x => x.Key.Equals(pluginContext))
                .Select(x => x.Value)
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values);
        }

        /// <summary>
        /// Returns the page items associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The application context to retrieve page items for.</param>
        /// <returns>An IEnumerable of <see cref="PageItem"/> associated with the specified application context.</returns>
        public IEnumerable<PageItem> GetPageItems(IApplicationContext applicationContext)
        {
            return _dict.Values
                .SelectMany(x => x)
                .Where(x => x.Key.Equals(applicationContext))
                .SelectMany(x => x.Value)
                .Select(x => x.Value);
        }

        /// <summary>
        /// Returns the page item associated with the specified page context.
        /// </summary>
        /// <param name="pageContext">The context of the page to retrieve.</param>
        /// <returns>The <see cref="PageItem"/> associated with the specified page context, or null if no such item exists.</returns>
        public PageItem GetPageItem(IPageContext pageContext)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .FirstOrDefault(x => x.PageContext.Equals(pageContext));
        }

        /// <summary>
        /// Returns the page items from the dictionary for a specific application context and page type.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="pageType">The type of the page.</param>
        /// <returns>An IEnumerable of page items.</returns>
        public IEnumerable<PageItem> GetPageItems(IApplicationContext applicationContext, Type pageType)
        {
            if (!typeof(IPage).IsAssignableFrom(pageType))
            {
                return [];
            }

            if (_dict.ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = _dict[applicationContext?.PluginContext];

                if (appContextDict.TryGetValue(applicationContext, out Dictionary<Type, PageItem> pageDict))
                {
                    if (pageDict.TryGetValue(pageType, out PageItem value))
                    {
                        return [value];
                    }
                }
            }

            return [];
        }

        /// <summary>
        /// Returns an enumeration of all containing page contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose pages are to be registered.</param>
        /// <returns>An enumeration of page contexts.</returns>
        public IEnumerable<IPageContext> GetPages(IPluginContext pluginContext)
        {
            if (_dict.TryGetValue(pluginContext, out var pluginResources))
            {
                return pluginResources
                    .SelectMany(x => x.Value)
                    .Select(x => x.Value.PageContext);
            }

            return [];
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <param name="pageType">The page type.</param>
        /// <returns>An enumeration of page contextes.</returns>
        public IEnumerable<IPageContext> GetPages(Type pageType)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.PageClass.Equals(pageType))
                .Select(x => x.PageContext);
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <param name="pageType">The page type.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of page contextes.</returns>
        public IEnumerable<IPageContext> GetPages(Type pageType, IApplicationContext applicationContext)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.PageClass.Equals(pageType))
                .Where(x => x.PageContext.ApplicationContext.Equals(applicationContext))
                .Select(x => x.PageContext);
        }

        /// <summary>
        /// Returns an enumeration of page contextes.
        /// </summary>
        /// <typeparam name="TPage">The page type.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of page contextes.</returns>
        public IEnumerable<IPageContext> GetPages<TPage>(IApplicationContext applicationContext) where TPage : IPage
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.PageClass.Equals(typeof(TPage)))
                .Where(x => x.PageContext.ApplicationContext.Equals(applicationContext))
                .Select(x => x.PageContext);
        }

        /// <summary>
        /// Returns the page context.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="pageId">The page id.</param>
        /// <returns>An page context or null.</returns>
        public IPageContext GetPage(IApplicationContext applicationContext, string pageId)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.PageContext.ApplicationContext.Equals(applicationContext))
                .Where(x => x.PageContext.EndpointId.Equals(pageId))
                .Select(x => x.PageContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the page context.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <param name="pageId">The page id.</param>
        /// <returns>An page context or null.</returns>
        public IPageContext GetPage(string applicationId, string pageId)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.PageContext.ApplicationContext.ApplicationId.Equals(applicationId))
                .Where(x => x.PageContext.EndpointId.Equals(pageId))
                .Select(x => x.PageContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <returns>True if the plugin context exists in the dictionary, otherwise false.</returns>
        public bool Contains(IPluginContext pluginContext)
        {
            return _dict.ContainsKey(pluginContext);
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <param name="applicationContext">The application context to check for.</param>
        /// <returns>True if the plugin context and application context exist in the dictionary, otherwise false.</returns>
        public bool Contains(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            return _dict.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext);
        }
    }
}
