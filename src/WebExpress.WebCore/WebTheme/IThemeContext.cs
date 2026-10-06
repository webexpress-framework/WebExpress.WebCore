using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebTheme
{
    /// <summary>
    /// Represents the context for a theme in the web application.
    /// </summary>
    public interface IThemeContext : IContext
    {
        /// <summary>
        /// Gets the theme id.
        /// </summary>
        IComponentId ThemeId { get; }

        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the image associated with the theme.
        /// </summary>
        IRoute Image { get; }

        /// <summary>
        /// Gets the name of the theme.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the theme.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the mode of the theme.
        /// </summary>
        ThemeMode ThemeMode { get; }

        /// <summary>
        /// Gets the route resource for the css theme style.
        /// </summary>
        IRoute ThemeStyle { get; }
    }
}
