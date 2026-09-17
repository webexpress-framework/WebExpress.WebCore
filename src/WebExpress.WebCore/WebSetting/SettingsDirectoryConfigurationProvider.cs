using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Merges every json file of a directory into one set of configuration values. Files are
    /// merged in alphabetical order of their names, the main file last, so precedence is
    /// predictable from a directory listing alone.
    /// </summary>
    /// <remarks>
    /// Each file is parsed by the regular json provider, so the files follow the same rules as an
    /// <c>appsettings.json</c> - comments and trailing commas included. A load builds the complete
    /// new set first and swaps it in only when every file could be read, so a file that is being
    /// saved at that moment never leaves the configuration half-updated.
    /// </remarks>
    public sealed class SettingsDirectoryConfigurationProvider : ConfigurationProvider, IDisposable
    {
        private readonly SettingsDirectoryConfigurationSource _source;
        private readonly PhysicalFileProvider _fileProvider;
        private readonly IDisposable _changeRegistration;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="source">The source describing the directory.</param>
        public SettingsDirectoryConfigurationProvider(SettingsDirectoryConfigurationSource source)
        {
            _source = source;

            // the watcher needs an existing directory; a missing one has nothing to watch and is
            // reported by the load instead
            if (source.ReloadOnChange && Directory.Exists(source.Path))
            {
                _fileProvider = new PhysicalFileProvider(source.Path);
                _changeRegistration = ChangeToken.OnChange(() => _fileProvider.Watch("*.json"), ReloadOnChange);
            }
        }

        /// <summary>
        /// Reads all json files of the directory. A file that cannot be read fails the whole load,
        /// so a broken file is noticed at start-up rather than silently ignored.
        /// </summary>
        public override void Load()
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in EnumerateFiles())
            {
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(file, optional: false, reloadOnChange: false)
                    .Build();

                using (configuration as IDisposable)
                {
                    // sections without a value are only containers; the keys below them carry the
                    // values, so copying them would add nothing but empty entries
                    foreach (var (key, value) in configuration.AsEnumerable().Where(x => x.Value is not null))
                    {
                        data[key] = value;
                    }
                }
            }

            Data = data;
        }

        /// <summary>
        /// Lists the files to merge in the order of their precedence, lowest first.
        /// </summary>
        /// <returns>The full paths of the files.</returns>
        public IEnumerable<string> EnumerateFiles()
        {
            if (string.IsNullOrWhiteSpace(_source.Path) || !Directory.Exists(_source.Path))
            {
                return [];
            }

            var files = Directory.EnumerateFiles(_source.Path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(x => Path.GetExtension(x).Equals(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var main = files.FirstOrDefault(x => Path.GetFileName(x).Equals(_source.MainFile, StringComparison.OrdinalIgnoreCase));

            if (main is not null)
            {
                files.Remove(main);
                files.Add(main);
            }

            return files;
        }

        /// <summary>
        /// Releases the file watcher.
        /// </summary>
        public void Dispose()
        {
            _changeRegistration?.Dispose();
            _fileProvider?.Dispose();
        }

        /// <summary>
        /// Reloads after a file changed. The previous values stay in place when the reload fails,
        /// because the failure happens on the watcher's thread where nobody could catch it.
        /// </summary>
        private void ReloadOnChange()
        {
            Thread.Sleep(_source.ReloadDelay);

            try
            {
                Load();
                OnReload();
            }
            catch (Exception ex)
            {
                _source.OnLoadException?.Invoke(ex);
            }
        }
    }
}
