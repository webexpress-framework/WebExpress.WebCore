using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;

namespace WebExpress.WebCore.WebSettingPage.Model
{
    /// <summary>
    /// Represents a dictionary for managing setting categories, organized by plugin and application contexts.
    /// </summary>
    internal class SettingCategoryDictionary : SettingItemRegistry<SettingCategoryItem>
    {
        /// <summary>
        /// Gets the collection of setting categories.
        /// </summary>
        public IEnumerable<ISettingCategoryContext> All => [.. Items().Select(x => x.SettingCategoryContext)];

        /// <summary>
        /// Adds a setting category.
        /// </summary>
        /// <param name="item">The setting category to insert.</param>
        /// <returns>True if the setting category was added successfully; otherwise, false.</returns>
        public bool AddSettingCategoryItem(SettingCategoryItem item)
        {
            return Add(item.PluginContext, item.ApplicationContext, item);
        }

        /// <summary>
        /// Returns the setting categories of an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of setting category contexts.</returns>
        public IEnumerable<ISettingCategoryContext> GetSettingCategories(IApplicationContext applicationContext)
        {
            return [.. Items(applicationContext).Select(x => x.SettingCategoryContext)];
        }

        /// <summary>
        /// Returns the setting category of a given class in an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="categoryType">The category class.</param>
        /// <returns>The setting category context, or null.</returns>
        public ISettingCategoryContext GetSettingCategory(IApplicationContext applicationContext, Type categoryType)
        {
            return Items(applicationContext)
                .Where(x => x.SettingCategoryClass == categoryType)
                .Select(x => x.SettingCategoryContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the registered category class of an application that bears the name of the
        /// given class, whether or not it is the same type.
        /// </summary>
        /// <remarks>
        /// Tells a category from a second copy of its assembly apart from one that is not
        /// registered at all.
        /// </remarks>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="categoryType">The category class.</param>
        /// <returns>The registered class of that name, or null.</returns>
        public Type GetSettingCategoryClassByName(IApplicationContext applicationContext, Type categoryType)
        {
            return Items(applicationContext)
                .Select(x => x.SettingCategoryClass)
                .FirstOrDefault(x => x.FullName == categoryType?.FullName);
        }
    }
}
