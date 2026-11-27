using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;

namespace WebExpress.WebCore.WebSettingPage.Model
{
    /// <summary>
    /// Represents a dictionary for managing setting groups, organized by plugin and application contexts.
    /// </summary>
    internal class SettingGroupDictionary : SettingItemRegistry<SettingGroupItem>
    {
        /// <summary>
        /// Gets the collection of setting groups.
        /// </summary>
        public IEnumerable<ISettingGroupContext> All => [.. Items().Select(x => x.SettingGroupContext)];

        /// <summary>
        /// Adds a setting group.
        /// </summary>
        /// <param name="item">The setting group to insert.</param>
        /// <returns>True if the setting group was added successfully; otherwise, false.</returns>
        public bool AddSettingGroupItem(SettingGroupItem item)
        {
            return Add(item.PluginContext, item.ApplicationContext, item);
        }

        /// <summary>
        /// Returns the setting groups of an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of setting group contexts.</returns>
        public IEnumerable<ISettingGroupContext> GetSettingGroups(IApplicationContext applicationContext)
        {
            return [.. Items(applicationContext).Select(x => x.SettingGroupContext)];
        }

        /// <summary>
        /// Returns the setting groups of a category in an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="categoryContext">The category.</param>
        /// <returns>An enumeration of setting group contexts.</returns>
        public IEnumerable<ISettingGroupContext> GetSettingGroups(IApplicationContext applicationContext, ISettingCategoryContext categoryContext)
        {
            return [.. Items(applicationContext)
                .Where(x => x.SettingGroupContext.SettingCategory == categoryContext)
                .Select(x => x.SettingGroupContext)];
        }

        /// <summary>
        /// Returns the setting group of a given class in an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="groupType">The group class.</param>
        /// <returns>The setting group context, or null.</returns>
        public ISettingGroupContext GetSettingGroup(IApplicationContext applicationContext, Type groupType)
        {
            return Items(applicationContext)
                .Where(x => x.SettingGroupClass == groupType)
                .Select(x => x.SettingGroupContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the registered group class of an application that bears the name of the
        /// given class, whether or not it is the same type.
        /// </summary>
        /// <remarks>
        /// A plugin that carries its own copy of a framework assembly sees a type of the same
        /// name that is not the registered one; finding it tells that case apart from a group
        /// that is not registered at all.
        /// </remarks>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="groupType">The group class.</param>
        /// <returns>The registered class of that name, or null.</returns>
        public Type GetSettingGroupClassByName(IApplicationContext applicationContext, Type groupType)
        {
            return Items(applicationContext)
                .Select(x => x.SettingGroupClass)
                .FirstOrDefault(x => x.FullName == groupType?.FullName);
        }
    }
}
