using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebSettingPage
{
    /// <summary>
    /// Read-only descriptor of a settings group — the heading under which related setting pages are
    /// listed in the settings navigation — exposing the application and plugin it belongs to.
    /// </summary>
    public interface ISettingGroupContext : IContext
    {
        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the setting category context to which the setting group belongs.
        /// </summary>
        ISettingCategoryContext SettingCategory { get; }

        /// <summary>
        /// Gets the group id.
        /// </summary>
        IComponentId GroupId { get; }

        /// <summary>
        /// Gets the icon.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the section.
        /// </summary>
        SettingSection Section { get; }
    }
}
