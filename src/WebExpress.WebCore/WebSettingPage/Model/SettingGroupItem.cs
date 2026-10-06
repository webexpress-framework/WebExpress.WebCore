using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebSettingPage.Model
{
    /// <summary>
    /// Internal record for one settings group — a heading within a category that bundles related
    /// setting pages. Links the group to its application and plugin.
    /// </summary>
    public class SettingGroupItem : IDisposable
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
        /// Gets the setting group context.
        /// </summary>
        public ISettingGroupContext SettingGroupContext { get; internal set; }

        /// <summary>
        /// Gets the class type of the setting group.
        /// </summary>
        public Type SettingGroupClass { get; internal set; }

        /// <summary>
        /// Gets the human-readable name or a internationalization key of the group.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the human-readable description or a internationalization key of the group. 
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the setting category.
        /// </summary>
        public Type Category { get; internal set; }

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
