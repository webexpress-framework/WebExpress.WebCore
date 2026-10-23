using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Adds the settings directory of the server to a configuration builder, in the same way the
    /// framework's own <c>AddJsonFile</c> adds a single file.
    /// </summary>
    public static class SettingsDirectoryConfigurationExtensions
    {
        /// <summary>
        /// Adds every json file of the given directory as one configuration source.
        /// </summary>
        /// <param name="builder">The configuration builder.</param>
        /// <param name="path">The settings directory.</param>
        /// <param name="mainFile">
        /// The file name of the main settings file, which is merged last and therefore overrides
        /// every other file. Defaults to <see cref="SettingsLoader.DefaultMainFile"/>.
        /// </param>
        /// <param name="reloadOnChange">Whether a change to a file reloads the configuration.</param>
        /// <param name="onLoadException">Called when a reload triggered by a file change fails.</param>
        /// <returns>The configuration builder, for chaining.</returns>
        public static IConfigurationBuilder AddSettingsDirectory(this IConfigurationBuilder builder, string path, string mainFile = null, bool reloadOnChange = true, Action<Exception> onLoadException = null)
        {
            return builder.Add(new SettingsDirectoryConfigurationSource
            {
                Path = Path.GetFullPath(path),
                MainFile = string.IsNullOrWhiteSpace(mainFile) ? SettingsLoader.DefaultMainFile : mainFile,
                ReloadOnChange = reloadOnChange,
                OnLoadException = onLoadException
            });
        }
    }
}
