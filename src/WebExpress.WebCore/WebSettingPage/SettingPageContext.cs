using System;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebSettingPage
{
    /// <summary>
    /// Interface representing the context of a setting page.
    /// Provides access to plugin context, application context, conditions for activation, and caching behavior.
    /// </summary>
    public class SettingPageContext : PageContext, ISettingPageContext
    {
        /// <summary>
        /// Gets the setting category context to which the setting page belongs.
        /// </summary>
        public ISettingCategoryContext SettingCategory => SettingGroup?.SettingCategory;

        /// <summary>
        /// Gets the group context to which the setting page belongs, or null while no plugin
        /// of the application provides the group.
        /// </summary>
        /// <remarks>
        /// Resolved on every read: the group often lives in another plugin (a framework group
        /// such as the general settings), and the plugins of an application register in no
        /// fixed order. A group taken once at registration would stay missing for good when the
        /// page happened to register first.
        /// </remarks>
        public ISettingGroupContext SettingGroup => ResolveSettingGroup?.Invoke();

        /// <summary>
        /// Gets or sets the lookup of the group in the current registrations.
        /// </summary>
        internal Func<ISettingGroupContext> ResolveSettingGroup { get; set; }

        /// <summary>  
        /// Gets the section of the setting page.  
        /// </summary>  
        public SettingSection Section { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the page should be displayed or hidden.
        /// </summary>
        public bool Hide { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the class with the specified parent type and context path.
        /// </summary>
        public SettingPageContext()
            : base()
        {
        }
    }
}
