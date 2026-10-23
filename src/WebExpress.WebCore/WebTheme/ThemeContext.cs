using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebTheme
{
    /// <summary>
    /// Represents the context for a theme in the web application.
    /// </summary>
    public class ThemeContext : IThemeContext
    {
        /// <summary>
        /// Gets the theme id.
        /// </summary>
        public IComponentId ThemeId { get; internal set; }

        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the image associated with the theme.
        /// </summary>
        public IRoute Image { get; internal set; }

        /// <summary>
        /// Gets the name of the theme.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the description of the theme.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the mode of the theme.
        /// </summary>
        public ThemeMode ThemeMode { get; internal set; }

        /// <summary>
        /// Gets the route resource for the css theme style.
        /// </summary>
        public IRoute ThemeStyle { get; internal set; }
    }
}
