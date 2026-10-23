using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using WebExpress.WebCore.WebPackage.Model;

namespace WebExpress.WebCore.WebPackage
{
    /// <summary>
    /// Class for generating webexpress packages from a build environment.
    /// </summary>
    public static class PackageBuilder
    {
        /// <summary>
        /// The directory inside a package that holds the settings files.
        /// </summary>
        public const string SettingsDirectory = "settings";

        /// <summary>
        /// Creates a webex package.
        /// </summary>
        /// <param name="specFile">The spec file (*.spec).</param>
        /// <param name="config">The config. Debug or Release.</param>
        /// <param name="targets">The target frameworks. Semicolon separated list of target framework moniker (TFM).</param>
        /// <param name="outputDirectory">The output directory.</param>
        public static void Create(string specFile, string config, string targets, string outputDirectory)
        {
            Console.WriteLine($"*** PackageBuilder: specFile '{specFile}'.");
            Console.WriteLine($"*** PackageBuilder: config '{config}'.");
            Console.WriteLine($"*** PackageBuilder: targets '{targets}'.");
            Console.WriteLine($"*** PackageBuilder: outputDirectory '{outputDirectory}'.");

            // validate input paths
            if (string.IsNullOrWhiteSpace(specFile))
            {
                throw new ArgumentException("specFile must not be null or empty.", nameof(specFile));
            }

            if (!File.Exists(specFile))
            {
                throw new FileNotFoundException("The specified spec file does not exist.", specFile);
            }

            var rootDirectory = Path.GetDirectoryName(specFile);

            // secure XML deserialization by prohibiting DTD processing
            using var fileStream = File.OpenRead(specFile);
            var serializer = new XmlSerializer(typeof(PackageItemSpec));
            using var xmlReader = XmlReader.Create(fileStream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });

            var package = (PackageItemSpec)serializer.Deserialize(xmlReader) ??
                throw new InvalidOperationException("Failed to deserialize the spec file.");
            var isCorePackage = string.Equals(package.Id, "WebExpress", StringComparison.Ordinal);
            var zipFileType = isCorePackage ? "zip" : "wxp";

            Console.WriteLine($"*** PackageBuilder: Creates a webex package '{package.Id}' in directory '{outputDirectory}'.");

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("outputDirectory must not be null or empty.", nameof(outputDirectory));
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // sanitize output archive file name components
            var safeId = SanitizeFileNameComponent(package.Id);
            var safeVersion = SanitizeFileNameComponent(package.Version);
            var archiveFilePath = Path.Combine(outputDirectory, $"{safeId}.{safeVersion}.{zipFileType}");

            using var zipFileStream = new FileStream(archiveFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, true);

            // find readme
            if (!string.IsNullOrWhiteSpace(package.Readme))
            {
                var file = Find(rootDirectory, Path.GetFileName(package.Readme));
                if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                {
                    ReadmeToZip(archive, file);
                }
            }

            // privacy policy
            if (!string.IsNullOrWhiteSpace(package.PrivacyPolicy))
            {
                var file = Find(rootDirectory, Path.GetFileName(package.PrivacyPolicy));
                if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                {
                    PrivacyPolicyToZip(archive, file);
                }
            }

            // find icon
            if (!string.IsNullOrWhiteSpace(package.Icon))
            {
                var file = Find(rootDirectory, Path.GetFileName(package.Icon));
                if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                {
                    IconToZip(archive, file);
                }
            }

            // find licenses
            if (!string.IsNullOrWhiteSpace(rootDirectory) && Directory.Exists(rootDirectory))
            {
                try
                {
                    foreach (var licFilePath in Directory.GetFiles(rootDirectory, "*.lic", SearchOption.AllDirectories)
                        .Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x)))
                    {
                        LicensesToZip(archive, licFilePath);
                    }
                }
                catch (Exception)
                {
                    // ignore errors while enumerating license files
                }
            }

            if (!isCorePackage)
            {
                SpecToZip(archive, package);
            }

            ProjectToZip(archive, package, rootDirectory, config, targets);
            ArtifactsToZip(archive, package, rootDirectory);
            SettingsToZip(archive, package, rootDirectory);
        }

        /// <summary>
        /// Create the readme file.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="filePath">The readme file path.</param>
        private static void ReadmeToZip(ZipArchive archive, string filePath)
        {
            // always use fixed entry name and stream file contents
            AddFileToZip(archive, "readme.md", filePath);
            Console.WriteLine($"*** PackageBuilder: Create the readme file.");
        }

        /// <summary>
        /// Create the privacy policy file.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="filePath">The privacy policy file path.</param>
        private static void PrivacyPolicyToZip(ZipArchive archive, string filePath)
        {
            // always use fixed entry name and stream file contents
            AddFileToZip(archive, "privacypolicy.md", filePath);
            Console.WriteLine($"*** PackageBuilder: Create the privacy policy file.");
        }

        /// <summary>
        /// Create the icon file.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="filePath">The icon file path.</param>
        private static void IconToZip(ZipArchive archive, string filePath)
        {
            // build entry name with original extension
            var ext = Path.GetExtension(filePath);
            var entryName = $"icon{ext}";
            AddFileToZip(archive, entryName, filePath);
            Console.WriteLine($"*** PackageBuilder: Create the icon file.");
        }

        /// <summary>
        /// Create the licenses file.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="filePath">The licenses file path.</param>
        private static void LicensesToZip(ZipArchive archive, string filePath)
        {
            // convert license file to a normalized .txt entry name while keeping contents as-is
            var name = Path.GetFileNameWithoutExtension(filePath);
            var entryName = $"licenses/{name}.txt";
            AddFileToZip(archive, entryName, filePath);
            Console.WriteLine($"*** PackageBuilder: Create the licenses file.");
        }

        /// <summary>
        /// Create the spec file.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="package">The package.</param>
        private static void SpecToZip(ZipArchive archive, PackageItemSpec package)
        {
            var zipBinarys = string.Equals(package?.Id, "WebExpress", StringComparison.Ordinal) ? "bin" : "lib";
            var specEntryName = $"{SanitizeFileNameComponent(package?.Id)}.spec";
            var sanitizedEntryName = SanitizeEntryPath(specEntryName);

            var zipArchiveEntry = archive.CreateEntry(sanitizedEntryName, CompressionLevel.Fastest);
            var serializer = new XmlSerializer(typeof(PackageItemSpec));
            using var zipStream = zipArchiveEntry.Open();

            // safely derive icon name if present
            var iconName = !string.IsNullOrWhiteSpace(package?.Icon) ? $"icon{Path.GetExtension(package.Icon)}" : null;

            var newPackage = new PackageItemSpec()
            {
                Id = package?.Id,
                Version = package?.Version,
                Title = package?.Title,
                Authors = package?.Authors,
                License = package?.License,
                LicenseUrl = package?.LicenseUrl,
                Icon = iconName,
                Readme = "readme.md",
                Description = package?.Description,
                Tags = package?.Tags,
                Plugins = package?.Plugins?.Select(x => $"{zipBinarys}/{SanitizeFileNameComponent(Path.GetFileName(x))}").ToArray(),
                Dependencies = package?.Dependencies,
                Settings = package?.Settings?.Select(x => $"{SettingsDirectory}/{SanitizeFileNameComponent(Path.GetFileName(x))}").ToArray()
            };

            serializer.Serialize(zipStream, newPackage);

            Console.WriteLine($"*** PackageBuilder: Create the spec file.");
        }

        /// <summary>
        /// Copy the plugin lib files to zip.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="package">The package.</param>
        /// <param name="path">The root path.</param>
        /// <param name="config">The config. Debug or Release.</param>
        /// <param name="targets">The target frameworks. Semicolon separated list of target framework moniker (TFM).</param>
        private static void ProjectToZip(ZipArchive archive, PackageItemSpec package, string path, string config, string targets)
        {
            var zipBinarys = string.Equals(package?.Id, "WebExpress", StringComparison.Ordinal) ? "bin" : "lib";

            foreach (var plugin in package?.Plugins ?? Enumerable.Empty<string>())
            {
                var pluginName = Path.GetFileName(plugin);
                var safePluginName = SanitizeFileNameComponent(pluginName);

                foreach (var target in targets?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>())
                {
                    var safeTarget = SanitizeFileNameComponent(target);
                    var dir = Path.Combine(path ?? string.Empty, plugin, "bin", config ?? string.Empty, target);

                    if (!Directory.Exists(dir))
                    {
                        // skip missing output directories
                        continue;
                    }

                    string[] files = [];
                    try
                    {
                        files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                    }
                    catch (Exception)
                    {
                        // ignore errors while enumerating plugin output files
                        continue;
                    }

                    foreach (var fileName in files.Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x)))
                    {
                        // compute relative path robustly
                        string relativePath;
                        try
                        {
                            relativePath = Path.GetRelativePath(dir, fileName);
                        }
                        catch
                        {
                            // fallback to file name if relative path fails
                            relativePath = Path.GetFileName(fileName);
                        }

                        var entryPathRaw = $"{zipBinarys}/{safePluginName}/{safeTarget}/{relativePath}";
                        var entryPath = SanitizeEntryPath(entryPathRaw);

                        AddFileToZip(archive, entryPath, fileName);

                        Console.WriteLine($"*** PackageBuilder: Copy the output file '{relativePath}' to {safePluginName}.");
                    }
                }
            }
        }

        /// <summary>
        /// Create the artifact files.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="package">The package.</param>
        /// <param name="path">The root path.</param>
        private static void ArtifactsToZip(ZipArchive archive, PackageItemSpec package, string path)
        {
            var zipBinarys = string.Equals(package?.Id, "WebExpress", StringComparison.Ordinal) ? "bin" : "lib";

            foreach (var item in package?.Artifacts ?? Enumerable.Empty<string>())
            {
                var fileName = Find(path, item);

                if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
                {
                    // preserve relative subpaths in artifacts while ensuring safe zip paths
                    var entryPath = SanitizeEntryPath($"{zipBinarys}/{item}");
                    AddFileToZip(archive, entryPath, fileName);

                    Console.WriteLine($"*** PackageBuilder: Create the artifact file '{fileName}'.");
                }
            }
        }

        /// <summary>
        /// Create the settings files. They are kept apart from the libraries under their own
        /// directory, so the package manager can deploy them to the settings directory of the
        /// server without extracting them next to the code.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="package">The package.</param>
        /// <param name="path">The root path.</param>
        private static void SettingsToZip(ZipArchive archive, PackageItemSpec package, string path)
        {
            foreach (var item in package?.Settings ?? Enumerable.Empty<string>())
            {
                var fileName = Find(path, item);

                if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
                {
                    var entryPath = SanitizeEntryPath($"{SettingsDirectory}/{Path.GetFileName(fileName)}");
                    AddFileToZip(archive, entryPath, fileName);

                    Console.WriteLine($"*** PackageBuilder: Create the settings file '{fileName}'.");
                }
            }
        }

        /// <summary>
        /// Find a file by walking up the directory tree and searching recursively at each level.
        /// </summary>
        /// <param name="path">The starting path.</param>
        /// <param name="fileName">The file name or trailing path to match.</param>
        /// <returns>The file name, if found; otherwise null.</returns>
        private static string Find(string path, string fileName)
        {
            // validate input
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var normalizedTail = fileName.Replace('\\', '/');

            // walk upwards until root
            while (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var matches = Directory
                        .GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Select(x => x.Replace('\\', '/'))
                        .Where(x => x.EndsWith(normalizedTail, StringComparison.OrdinalIgnoreCase));

                    foreach (var f in matches)
                    {
                        return f;
                    }
                }
                catch (Exception)
                {
                    // ignore errors while enumerating and continue with parent
                }

                try
                {
                    path = Directory.GetParent(path)?.FullName;
                }
                catch
                {
                    path = null;
                }
            }

            return null;
        }

        /// <summary>
        /// Adds a file to the zip archive using a sanitized entry path and streams the file contents.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="entryPath">The entry path inside the zip archive.</param>
        /// <param name="sourceFilePath">The source file path to read.</param>
        private static void AddFileToZip(ZipArchive archive, string entryPath, string sourceFilePath)
        {
            // sanitize entry path to prevent zip-slip
            var safeEntry = SanitizeEntryPath(entryPath);
            var zipArchiveEntry = archive.CreateEntry(safeEntry, CompressionLevel.Fastest);

            using var zipStream = zipArchiveEntry.Open();
            using var fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.CopyTo(zipStream);
        }

        /// <summary>
        /// Sanitizes a filename component by removing invalid characters and path separators.
        /// </summary>
        /// <param name="name">The filename component.</param>
        /// <returns>A sanitized filename component safe for file and zip entry names.</returns>
        private static string SanitizeFileNameComponent(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);

            foreach (var ch in name)
            {
                var isInvalid = Array.IndexOf(invalid, ch) >= 0 || ch == '/' || ch == '\\' || ch == ':';
                if (isInvalid)
                {
                    // replace invalid characters with underscore
                    sb.Append('_');
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Sanitizes a ZIP entry path by normalizing separators and removing dangerous segments like "..".
        /// </summary>
        /// <param name="entryPath">The raw entry path.</param>
        /// <returns>A sanitized entry path safe for inclusion in a zip archive.</returns>
        private static string SanitizeEntryPath(string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                return string.Empty;
            }

            // normalize to forward slashes
            var path = entryPath.Replace('\\', '/');

            // remove drive letters and leading slashes
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            {
                path = path[2..];
            }

            path = path.TrimStart('/');

            // split and filter segments
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var safeSegments = segments
                .Where(s => s != "." && s != "..")
                .Select(SanitizeFileNameComponent)
                .Where(s => !string.IsNullOrWhiteSpace(s));

            // join back with forward slashes
            return string.Join("/", safeSegments);
        }
    }
}
