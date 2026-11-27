using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;

namespace WebExpress.WebCore.WebSettingPage.Model
{
    /// <summary>
    /// Represents a dictionary for managing setting pages, organized by plugin and application contexts.
    /// </summary>
    internal class SettingPageDictionary : SettingItemRegistry<SettingPageItem>
    {
        /// <summary>
        /// Gets the collection of setting pages.
        /// </summary>
        public IEnumerable<ISettingPageContext> All => [.. Items().Select(x => x.SettingPageContext)];

        /// <summary>
        /// Adds a settings page.
        /// </summary>
        /// <param name="item">The settings page to insert.</param>
        /// <returns>True if the settings page was added successfully; otherwise, false.</returns>
        public bool AddSettingPageItem(SettingPageItem item)
        {
            return Add(item.PluginContext, item.ApplicationContext, item);
        }

        /// <summary>
        /// Returns an enumeration of setting page contextes.
        /// </summary>
        /// <param name="settingPageType">The setting page type.</param>
        /// <returns>An enumeration of setting page contextes.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(Type settingPageType)
        {
            return [.. Items()
                .Where(x => x.SettingPageClass.Equals(settingPageType))
                .Select(x => x.SettingPageContext)];
        }

        /// <summary>
        /// Returns an enumeration of setting page contextes.
        /// </summary>
        /// <param name="settingPageType">The setting page type.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of setting page contextes.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(Type settingPageType, IApplicationContext applicationContext)
        {
            return [.. Items(applicationContext)
                .Where(x => x.SettingPageClass.Equals(settingPageType))
                .Select(x => x.SettingPageContext)];
        }

        /// <summary>
        /// Creates a new setting page instance or returns an existing cached instance.
        /// </summary>
        /// <remarks>
        /// The instance is created outside the lock, so a slow page constructor does not hold up
        /// other requests; when two requests create a cached page at once, both use the one kept.
        /// </remarks>
        /// <param name="settingPageContext">The context used for setting page creation.</param>
        /// <param name="componentHub">The central management hub for components.</param>
        /// <param name="httpServerContext">The context of the HTTP server.</param>
        /// <returns>The created or cached setting page instance.</returns>
        public IEndpoint CreateSettingPageInstance(ISettingPageContext settingPageContext, IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            var settingPageItem = Items().FirstOrDefault(x => x.SettingPageContext.Equals(settingPageContext));

            if (settingPageItem is null)
            {
                return null;
            }

            var cached = WithItem(x => x == settingPageItem, x => x?.Instance);

            if (cached is not null)
            {
                return cached;
            }

            var instance = ComponentActivator.CreateInstance<IEndpoint, ISettingPageContext>
            (
                settingPageItem.SettingPageClass,
                settingPageContext,
                httpServerContext,
                componentHub,
                settingPageContext.ApplicationContext
            );

            if (!settingPageItem.Cache)
            {
                return instance;
            }

            return WithItem(x => x == settingPageItem, x =>
            {
                if (x is null)
                {
                    return instance;
                }

                x.Instance ??= instance;

                return x.Instance;
            });
        }

        /// <summary>
        /// Returns an enumeration of setting page contexts for the specified application context and category.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="categoryContext">The category for which to retrieve setting pages.</param>
        /// <returns>An enumeration of setting page contexts.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(IApplicationContext applicationContext, ISettingCategoryContext categoryContext)
        {
            return [.. Items(applicationContext)
                .Where(x => x.SettingPageContext?.SettingCategory == categoryContext)
                .Select(x => x.SettingPageContext)];
        }

        /// <summary>
        /// Returns an enumeration of setting page contexts for the specified application context, category, and group.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="groupContext">The group for which to retrieve setting pages.</param>
        /// <returns>An enumeration of setting page contexts.</returns>
        public IEnumerable<ISettingPageContext> GetSettingPages(IApplicationContext applicationContext, ISettingGroupContext groupContext)
        {
            return [.. Items(applicationContext)
                .Where(x => x.SettingPageContext?.SettingGroup == groupContext)
                .Select(x => x.SettingPageContext)];
        }
    }
}
