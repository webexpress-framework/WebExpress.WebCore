using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebSettingPage
{
    /// <summary>
    /// Provides context for setting group within the web setting page.
    /// </summary>
    public class SettingGroupContext : ISettingGroupContext
    {
        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the setting category context to which the setting group belongs, or null while
        /// no plugin of the application provides the category.
        /// </summary>
        /// <remarks>
        /// Resolved on every read for the same reason as <see cref="SettingPageContext.SettingGroup"/>:
        /// the category may come from a plugin that registers after the group.
        /// </remarks>
        public ISettingCategoryContext SettingCategory => ResolveSettingCategory?.Invoke();

        /// <summary>
        /// Gets or sets the lookup of the category in the current registrations.
        /// </summary>
        internal Func<ISettingCategoryContext> ResolveSettingCategory { get; set; }

        /// <summary>
        /// Gets the group id.
        /// </summary>
        public IComponentId GroupId { get; internal set; }

        /// <summary>
        /// Gets the icon.
        /// </summary>
        public IIcon Icon { get; internal set; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the section.
        /// </summary>
        public SettingSection Section { get; internal set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"Setting group: {GroupId}";
        }
    }
}
