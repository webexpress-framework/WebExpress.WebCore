using System;
using Microsoft.Extensions.Configuration;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Describes a directory of json files that together form one configuration: the settings
    /// directory of the server, into which every deployed plugin drops its own file.
    /// </summary>
    /// <remarks>
    /// A directory rather than a fixed list of files, because the set of files is not known when
    /// the server starts: a package installed at runtime adds one. Enumerating the directory on
    /// every load lets a later <see cref="IConfigurationRoot.Reload"/> pick it up without
    /// rebuilding the configuration everyone already holds a reference to.
    /// </remarks>
    public sealed class SettingsDirectoryConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// The directory whose json files are merged.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The file name of the main settings file within the directory. It is merged last, so
        /// its values take precedence over those of every other file - the one place an
        /// administrator can override a plugin's shipped default.
        /// </summary>
        public string MainFile { get; set; }

        /// <summary>
        /// Whether a change to any json file in the directory reloads the configuration.
        /// </summary>
        public bool ReloadOnChange { get; set; }

        /// <summary>
        /// The time to wait after a change before reloading, in milliseconds. Editors write a file
        /// in several steps, so reading it the instant the first change is seen would find it
        /// half-written.
        /// </summary>
        public int ReloadDelay { get; set; } = 250;

        /// <summary>
        /// Called when a reload triggered by a file change fails. The previous values are kept
        /// in that case; the callback is the only place the failure surfaces, because there is no
        /// caller to throw to on the watcher's thread.
        /// </summary>
        public Action<Exception> OnLoadException { get; set; }

        /// <summary>
        /// Builds the provider that reads the directory.
        /// </summary>
        /// <param name="builder">The configuration builder.</param>
        /// <returns>The provider.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new SettingsDirectoryConfigurationProvider(this);
        }
    }
}
