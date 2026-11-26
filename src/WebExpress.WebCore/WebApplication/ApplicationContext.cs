using System;
using System.Linq;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebTheme;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// Default implementation of <see cref="IApplicationContext"/>: the read-only descriptor of a
    /// registered application (id, name, paths, route, owning plugin, default theme) that the
    /// framework shares with the application's components.
    /// </summary>
    public class ApplicationContext : IApplicationContext
    {
        /// <summary>
        /// Gets the context of the associated plugin.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the application id.
        /// </summary>
        public string ApplicationId { get; internal set; }

        /// <summary>
        /// Gets the application name.
        /// </summary>
        public string ApplicationName { get; internal set; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the asset directory. This is mounted in the asset directory of the server.
        /// </summary>
        public string AssetPath { get; internal set; }

        /// <summary>
        /// Gets the data directory. This is mounted in the data directory of the server.
        /// </summary>
        public string DataPath { get; internal set; }

        /// <summary>
        /// Gets the context path. This is mounted in the route of the server.
        /// </summary>
        public string ContextPath { get; internal set; }

        /// <summary>
        /// Gets the context path. This is mounted in the route of the server.
        /// </summary>
        public IRoute Route { get; internal set; }

        private IRoute _icon;

        /// <summary>
        /// Gets the icon uri.
        /// </summary>
        /// <remarks>
        /// The icon is declared by its plain path, but every page shows it, so while it is a file
        /// of a plugin this is the address that carries the hash of its content, which the browser
        /// keeps for good. It is resolved on every read: the file is mounted only after the
        /// application is registered, and a plugin loaded again brings a new hash.
        /// </remarks>
        public IRoute Icon
        {
            get => (_icon is not null ? ResolveAsset?.Invoke(_icon.ToString()) : null) ?? _icon;
            internal set => _icon = value;
        }

        /// <summary>
        /// Gets the route of the icon as it is declared, before it is resolved to a versioned file.
        /// </summary>
        internal IRoute DeclaredIconRoute => _icon;

        /// <summary>
        /// Gets or sets the lookup of the versioned route of a file mounted on a plain route,
        /// supplied by the application manager of the hub the application belongs to.
        /// </summary>
        internal Func<string, IRoute> ResolveAsset { get; set; }

        /// <summary>
        /// Gets or sets the type of the theme declared via
        /// <c>[Theme&lt;TTheme&gt;]</c> on the application class. Internal
        /// because the public surface is the resolved
        /// <see cref="DefaultTheme"/>.
        /// </summary>
        internal Type DefaultThemeType { get; set; }

        /// <summary>
        /// Returns the theme context that corresponds to the type set via
        /// <c>[Theme&lt;TTheme&gt;]</c>. Resolved lazily through the active
        /// <see cref="WebTheme.IThemeManager"/> so the property reflects the
        /// current registration state. Returns <see langword="null"/> when
        /// the application did not declare a default theme or the declared
        /// theme has not (yet) been registered for this application.
        /// </summary>
        public IThemeContext DefaultTheme
        {
            get
            {
                if (DefaultThemeType is null)
                {
                    return null;
                }

                return WebEx.ComponentHub?.ThemeManager?.GetThemes(this, DefaultThemeType)?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ApplicationContext()
        {
        }

        /// <summary>
        /// Conversion of the application context into its string representation.
        /// </summary>
        /// <returns>The string that uniquely represents the application.</returns>
        public override string ToString()
        {
            return $"Application: {ApplicationId}";
        }
    }
}
