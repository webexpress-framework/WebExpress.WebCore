using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Builds the configuration of the server. It is the one place that knows which sources make
    /// up the configuration and in which order they override each other, so a host and a test
    /// end up with the same composition the server runs on.
    /// </summary>
    /// <remarks>
    /// The sources, lowest precedence first: every json file of the settings directory in
    /// alphabetical order, then the main settings file, then the environment variables carrying
    /// the <see cref="EnvironmentVariablePrefix"/>. A plugin ships its defaults in its own file,
    /// an administrator overrides them in the main file, and a container overrides both from the
    /// environment without touching a file at all.
    /// </remarks>
    public static class SettingsLoader
    {
        /// <summary>
        /// The name of the settings directory, relative to the working directory.
        /// </summary>
        public const string DefaultDirectory = "settings";

        /// <summary>
        /// The file name of the main settings file.
        /// </summary>
        public const string DefaultMainFile = "webexpress.settings.json";

        /// <summary>
        /// The prefix of the environment variables that override settings, e.g.
        /// <c>WEBEXPRESS_WebExpress__Culture</c>. The prefix keeps unrelated variables of the host
        /// out of the configuration.
        /// </summary>
        public const string EnvironmentVariablePrefix = "WEBEXPRESS_";

        /// <summary>
        /// Builds the configuration from the given main settings file, its directory and the
        /// environment.
        /// </summary>
        /// <param name="settingsFile">The path of the main settings file; its directory is the settings directory.</param>
        /// <param name="onLoadException">Called when a reload triggered by a file change fails.</param>
        /// <returns>The configuration.</returns>
        public static IConfigurationRoot Load(string settingsFile, Action<Exception> onLoadException = null)
        {
            var fullPath = Path.GetFullPath(settingsFile);

            return new ConfigurationBuilder()
                .AddSettingsDirectory(Path.GetDirectoryName(fullPath), Path.GetFileName(fullPath), onLoadException: onLoadException)
                .AddEnvironmentVariables(EnvironmentVariablePrefix)
                .Build();
        }

        /// <summary>
        /// Binds the server settings from the configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The server settings; every block that is absent in the configuration stays at its default.</returns>
        public static HttpServerSettings GetServerSettings(this IConfiguration configuration)
        {
            return configuration.GetSection(HttpServerSettings.Section).Get<HttpServerSettings>() ?? new HttpServerSettings();
        }

        /// <summary>
        /// Returns the section a plugin's own settings live in.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pluginId">The id of the plugin.</param>
        /// <returns>The section; it exists even when nothing is configured, then simply without values.</returns>
        public static IConfigurationSection GetPluginSettings(this IConfiguration configuration, string pluginId)
        {
            return configuration.GetSection(ConfigurationPath.Combine(HttpServerSettings.PluginSection, pluginId));
        }
    }
}
