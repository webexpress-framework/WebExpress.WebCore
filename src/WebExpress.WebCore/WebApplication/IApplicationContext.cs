using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebTheme;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// Read-only descriptor of a registered application that the framework hands to components so
    /// they can learn about the application they belong to — its id, name, description, asset/data
    /// paths, route, icon, owning plugin, and default theme — without referencing the application
    /// object itself.
    /// </summary>
    public interface IApplicationContext : IContext
    {
        /// <summary>
        /// Gets the context of the associated plugin.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the application id.
        /// </summary>
        string ApplicationId { get; }

        /// <summary>
        /// Gets the application name.
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the asset directory. This is mounted in the asset directory of the server.
        /// </summary>
        string AssetPath { get; }

        /// <summary>
        /// Gets the data directory. This is mounted in the data directory of the server.
        /// </summary>
        string DataPath { get; }

        /// <summary>
        /// Gets the context path. This is mounted in the route of the server.
        /// </summary>
        string ContextPath { get; }

        /// <summary>
        /// Gets the context path. This is mounted in the route of the server.
        /// </summary>
        IRoute Route { get; }

        /// <summary>
        /// Gets the icon uri.
        /// </summary>
        IRoute Icon { get; }

        /// <summary>
        /// Gets the default theme declared by the application via
        /// <c>[Theme&lt;TTheme&gt;]</c>. Resolves the matching
        /// <see cref="IThemeContext"/> through the active
        /// <c>ThemeManager</c> at read time, so the property reflects the
        /// current registration state. Returns <see langword="null"/> when
        /// the application did not declare a default theme or the declared
        /// theme has not (yet) been registered for this application.
        /// </summary>
        IThemeContext DefaultTheme { get; }
    }
}
