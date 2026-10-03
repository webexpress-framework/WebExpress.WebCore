using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebSettingPage.Model
{
    /// <summary>
    /// Internal record for one settings category — the top level of the settings navigation, which
    /// contains groups, which in turn contain pages. Links the category to its application and plugin.
    /// </summary>
    public class SettingCategoryItem : IDisposable
    {
        /// <summary>
        /// Gets the context of the associated plugin.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the setting category context.
        /// </summary>
        public ISettingCategoryContext SettingCategoryContext { get; internal set; }

        /// <summary>
        /// Gets the class type of the setting category.
        /// </summary>
        public Type SettingCategoryClass { get; internal set; }

        /// <summary>
        /// Gets the human-readable name or a internationalization key of the category.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the human-readable description or a internationalization key of the category. 
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the section.
        /// </summary>
        public SettingSection Section { get; internal set; }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
