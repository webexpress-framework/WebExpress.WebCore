using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using WebExpress.WebCore.WebPackage.Model;

namespace WebExpress.WebCore.WebPackage
{
    /// <summary>
    /// Class for generating webexpress packages from a build environment.
    /// </summary>
    /// <remarks>
    /// Every file named by the spec is resolved before the archive is written, so a spec that
    /// names a missing file fails the build instead of producing a package that breaks only
    /// when it is installed. Files and plugin outputs are looked up below the folder of the spec
    /// file and never outside of it, and build output only counts when it belongs to the requested
    /// configuration and target framework: a stale <c>bin/Debug</c> must not end up in a Release
    /// package, nor a <c>net8.0</c> build in a <c>net10.0</c> one.
    /// </remarks>
    public static class PackageBuilder
    {
        /// <summary>
        /// The directory inside a package that holds the settings files.
        /// </summary>
        public const string SettingsDirectory = "settings";

        /// <summary>
        /// Recognizes the output folder of a target framework moniker, e.g. net10.0, net8.0-windows,
        /// net472, netstandard2.0 or netcoreapp3.1, as opposed to an ordinary subfolder of the output.
        /// </summary>
        private static readonly Regex TargetFrameworkFolder = new(@"^net(standard|coreapp)?\d+(\.\d+)*(-.+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Creates a webex package.
        /// </summary>
        /// <param name="specFile">The spec file (*.spec).</param>
        /// <param name="config">The config. Debug or Release.</param>
        /// <param name="targets">The target frameworks. Semicolon separated list of target framework moniker (TFM).</param>
        /// <param name="outputDirectory">The output directory.</param>
        /// <exception cref="FileNotFoundException">
        /// The spec file, or a file or plugin output named by it, does not exist or the plugin output
        /// is empty. No archive is written.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// A plugin path of the spec leads outside of the spec folder. No archive is written.
        /// </exception>
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

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("outputDirectory must not be null or empty.", nameof(outputDirectory));
            }

            var rootDirectory = Path.GetDirectoryName(Path.GetFullPath(specFile));
            var package = ReadSpec(specFile);
            var isCorePackage = string.Equals(package.Id, "WebExpress", StringComparison.Ordinal);
            var zipFileType = isCorePackage ? "zip" : "wxp";
            var zipBinarys = isCorePackage ? "bin" : "lib";
            var targetList = targets?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

            Console.WriteLine($"*** PackageBuilder: Creates a webex package '{package.Id}' in directory '{outputDirectory}'.");

            var files = ListFiles(rootDirectory);
            var missing = new List<string>();

            var readme = Resolve(files, rootDirectory, package.Readme, "readme", config, targetList, missing);
            var privacyPolicy = Resolve(files, rootDirectory, package.PrivacyPolicy, "privacypolicy", config, targetList, missing);
            var icon = Resolve(files, rootDirectory, package.Icon, "icon", config, targetList, missing);
            var licenses = FindLicenses(files, rootDirectory);
            var artifacts = (package.Artifacts ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => (Item: x, File: Resolve(files, rootDirectory, x, "artifact", config, targetList, missing)))
                .ToList();
            var settings = (package.Settings ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => (Item: x, File: Resolve(files, rootDirectory, x, "settings", config, targetList, missing)))
                .ToList();
            var pluginOutputs = ResolvePluginOutputs(package, rootDirectory, config, targetList, missing);

            if (missing.Count > 0)
            {
                foreach (var item in missing)
                {
                    Console.WriteLine($"*** PackageBuilder: Not found: {item}.");
                }

                throw new FileNotFoundException($"The package '{package.Id}' was not created, because the spec names {missing.Count} file(s) that were not found: {string.Join("; ", missing)}.");
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // sanitize output archive file name components
            var safeId = SanitizeFileNameComponent(package.Id);
            var safeVersion = SanitizeFileNameComponent(package.Version);
            var archiveFilePath = Path.Combine(outputDirectory, $"{safeId}.{safeVersion}.{zipFileType}");

            try
            {
                using var zipFileStream = new FileStream(archiveFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, true);

                if (readme is not null)
                {
                    AddFileToZip(archive, "readme.md", readme);
                    Console.WriteLine($"*** PackageBuilder: Create the readme file '{readme}'.");
                }

                if (privacyPolicy is not null)
                {
                    AddFileToZip(archive, "privacypolicy.md", privacyPolicy);
                    Console.WriteLine($"*** PackageBuilder: Create the privacy policy file '{privacyPolicy}'.");
                }

                if (icon is not null)
                {
                    AddFileToZip(archive, $"icon{Path.GetExtension(icon)}", icon);
                    Console.WriteLine($"*** PackageBuilder: Create the icon file '{icon}'.");
                }

                foreach (var license in licenses)
                {
                    AddFileToZip(archive, $"licenses/{Path.GetFileNameWithoutExtension(license)}.txt", license);
                    Console.WriteLine($"*** PackageBuilder: Create the licenses file '{license}'.");
                }

                if (!isCorePackage)
                {
                    SpecToZip(archive, package, zipBinarys);
                }

                foreach (var (plugin, target, directory) in pluginOutputs)
                {
                    foreach (var fileName in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(directory, fileName);
                        AddFileToZip(archive, $"{zipBinarys}/{plugin}/{target}/{relativePath}", fileName);
                        Console.WriteLine($"*** PackageBuilder: Copy the output file '{relativePath}' to {plugin}.");
                    }
                }

                foreach (var (item, fileName) in artifacts)
                {
                    // preserve relative subpaths in artifacts while ensuring safe zip paths
                    AddFileToZip(archive, $"{zipBinarys}/{item}", fileName);
                    Console.WriteLine($"*** PackageBuilder: Create the artifact file '{fileName}'.");
                }

                foreach (var (_, fileName) in settings)
                {
                    // kept apart from the libraries, so the package manager can deploy them to the
                    // settings directory of the server without extracting them next to the code
                    AddFileToZip(archive, $"{SettingsDirectory}/{Path.GetFileName(fileName)}", fileName);
                    Console.WriteLine($"*** PackageBuilder: Create the settings file '{fileName}'.");
                }
            }
            catch
            {
                // a half written archive looks like a valid package to anyone who copies it on
                TryDelete(archiveFilePath);
                throw;
            }
        }

        /// <summary>
        /// Reads the spec file.
        /// </summary>
        /// <param name="specFile">The spec file (*.spec).</param>
        /// <returns>The package described by the spec file.</returns>
        private static PackageItemSpec ReadSpec(string specFile)
        {
            // secure XML deserialization by prohibiting DTD processing
            using var fileStream = File.OpenRead(specFile);
            var serializer = new XmlSerializer(typeof(PackageItemSpec));
            using var xmlReader = XmlReader.Create(fileStream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });

            return (PackageItemSpec)serializer.Deserialize(xmlReader) ??
                throw new InvalidOperationException("Failed to deserialize the spec file.");
        }

        /// <summary>
        /// Lists every file below the root directory once, so each lookup of the spec does not
        /// walk the tree again.
        /// </summary>
        /// <param name="rootDirectory">The folder of the spec file.</param>
        /// <returns>The paths relative to the root, with forward slashes.</returns>
        private static List<string> ListFiles(string rootDirectory)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            return [.. Directory
                .EnumerateFiles(rootDirectory, "*", options)
                .Select(x => Path.GetRelativePath(rootDirectory, x).Replace('\\', '/'))];
        }

        /// <summary>
        /// Resolves a file named by the spec and records it as missing when it cannot be found.
        /// </summary>
        /// <param name="files">The files below the root directory.</param>
        /// <param name="rootDirectory">The folder of the spec file.</param>
        /// <param name="item">The file name or trailing path from the spec; empty when the spec omits it.</param>
        /// <param name="kind">The spec element, used in the error message.</param>
        /// <param name="config">The build configuration.</param>
        /// <param name="targets">The target frameworks.</param>
        /// <param name="missing">Collects the items that were not found.</param>
        /// <returns>The full path of the file, or null if the spec omits it or it was not found.</returns>
        private static string Resolve(IReadOnlyList<string> files, string rootDirectory, string item, string kind, string config, IReadOnlyCollection<string> targets, List<string> missing)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                return null;
            }

            var file = Find(files, rootDirectory, item, config, targets);

            if (file is null)
            {
                missing.Add($"<{kind}>{item}</{kind}>");
            }

            return file;
        }

        /// <summary>
        /// Finds a file by its name or trailing path below the root directory.
        /// </summary>
        /// <remarks>
        /// The same file name usually exists several times in a build tree - in every
        /// configuration's output, in obj, and as the source item that was copied to the output.
        /// The candidates are ranked: output of the requested configuration and target first,
        /// then output of the requested configuration, then files outside of any build output;
        /// output of another configuration or target framework and anything in obj never qualifies. Within a rank,
        /// the shallowest path wins, so the readme in the root beats one in a subfolder.
        /// </remarks>
        /// <param name="files">The files below the root directory.</param>
        /// <param name="rootDirectory">The folder of the spec file.</param>
        /// <param name="fileName">The file name or trailing path to match.</param>
        /// <param name="config">The build configuration.</param>
        /// <param name="targets">The target frameworks.</param>
        /// <returns>The full path of the file, if found; otherwise null.</returns>
        private static string Find(IReadOnlyList<string> files, string rootDirectory, string fileName, string config, IReadOnlyCollection<string> targets)
        {
            // matching on a segment boundary keeps "WebExpress.dll" from hitting "MyWebExpress.dll"
            var tail = "/" + fileName.Replace('\\', '/').TrimStart('/');

            var match = files
                .Where(x => ("/" + x).EndsWith(tail, StringComparison.OrdinalIgnoreCase))
                .Select(x => (Path: x, Rank: Rank(x, config, targets)))
                .Where(x => x.Rank >= 0)
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Path.Count(c => c == '/'))
                .ThenBy(x => x.Path, StringComparer.Ordinal)
                .Select(x => x.Path)
                .FirstOrDefault();

            return match is null ? null : Path.GetFullPath(Path.Combine(rootDirectory, match));
        }

        /// <summary>
        /// Ranks a candidate file by where it sits in the build tree.
        /// </summary>
        /// <param name="relativePath">The path relative to the root directory, with forward slashes.</param>
        /// <param name="config">The build configuration.</param>
        /// <param name="targets">The target frameworks.</param>
        /// <returns>0 to 2, lower is better; -1 if the file must not be packed.</returns>
        private static int Rank(string relativePath, string config, IReadOnlyCollection<string> targets)
        {
            var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var directories = segments.Length - 1;

            for (var i = 0; i < directories; i++)
            {
                if (segments[i].Equals("obj", StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }

                if (segments[i].Equals("bin", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= directories || !segments[i + 1].Equals(config, StringComparison.OrdinalIgnoreCase))
                    {
                        return -1;
                    }

                    if (i + 2 < directories)
                    {
                        if (targets.Contains(segments[i + 2], StringComparer.OrdinalIgnoreCase))
                        {
                            return 0;
                        }

                        // a dll built for another framework may not even load on the requested one
                        if (TargetFrameworkFolder.IsMatch(segments[i + 2]))
                        {
                            return -1;
                        }
                    }

                    return 1;
                }
            }

            return 2;
        }

        /// <summary>
        /// Checks whether a path lies inside the root directory.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <param name="rootDirectory">The folder of the spec file.</param>
        /// <returns>True if the path is the root directory or below it.</returns>
        private static bool IsWithin(string path, string rootDirectory)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory)) + Path.DirectorySeparatorChar;
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;

            return full.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>
        /// Finds the license files (*.lic) of the package.
        /// </summary>
        /// <param name="files">The files below the root directory.</param>
        /// <param name="rootDirectory">The folder of the spec file.</param>
        /// <returns>The full paths, at most one per license name.</returns>
        private static List<string> FindLicenses(IReadOnlyList<string> files, string rootDirectory)
        {
            // build output may carry copies; they would add a second entry of the same name
            return [.. files
                .Where(x => x.EndsWith(".lic", StringComparison.OrdinalIgnoreCase))
                .Where(x => Rank(x, null, []) == 2)
                .OrderBy(x => x, StringComparer.Ordinal)
                .GroupBy(x => Path.GetFileNameWithoutExtension(x), StringComparer.OrdinalIgnoreCase)
                .Select(x => Path.GetFullPath(Path.Combine(rootDirectory, x.First())))];
        }

        /// <summary>
        /// Resolves the build output directories of the plugins named by the spec.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="rootDirectory">The folder of the spec file.</param>
        /// <param name="config">The build configuration.</param>
        /// <param name="targets">The target frameworks.</param>
        /// <param name="missing">Collects the plugin outputs that were not found or are empty.</param>
        /// <returns>One entry per plugin and target, with the sanitized names used inside the package.</returns>
        /// <exception cref="InvalidOperationException">A plugin path leads outside of the spec folder.</exception>
        private static List<(string Plugin, string Target, string Directory)> ResolvePluginOutputs(PackageItemSpec package, string rootDirectory, string config, IReadOnlyCollection<string> targets, List<string> missing)
        {
            var outputs = new List<(string, string, string)>();

            foreach (var plugin in (package.Plugins ?? []).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                // an absolute path or "../" would pull the build output of a foreign project into the package
                var pluginDirectory = Path.GetFullPath(Path.Combine(rootDirectory, plugin));

                if (!IsWithin(pluginDirectory, rootDirectory))
                {
                    throw new InvalidOperationException($"<plugin>{plugin}</plugin> points to '{pluginDirectory}', outside of the spec folder '{rootDirectory}'.");
                }

                var pluginName = PluginName(plugin);

                foreach (var target in targets)
                {
                    var directory = Path.Combine(pluginDirectory, "bin", config ?? string.Empty, target);

                    if (!Directory.Exists(directory))
                    {
                        missing.Add($"<plugin>{plugin}</plugin> (no build output in '{directory}')");
                    }
                    else if (!Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any())
                    {
                        // the spec of the package would name the plugin while the package carries none of its files
                        missing.Add($"<plugin>{plugin}</plugin> (build output in '{directory}' is empty)");
                    }
                    else
                    {
                        outputs.Add((pluginName, SanitizeFileNameComponent(target), directory));
                    }
                }
            }

            return outputs;
        }

        /// <summary>
        /// Create the spec file.
        /// </summary>
        /// <param name="archive">The zip archive.</param>
        /// <param name="package">The package.</param>
        /// <param name="zipBinarys">The directory of the binaries inside the package.</param>
        private static void SpecToZip(ZipArchive archive, PackageItemSpec package, string zipBinarys)
        {
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
                Plugins = package?.Plugins?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $"{zipBinarys}/{PluginName(x)}").ToArray(),
                Dependencies = package?.Dependencies,
                Settings = package?.Settings?.Select(x => $"{SettingsDirectory}/{SanitizeFileNameComponent(Path.GetFileName(x))}").ToArray()
            };

            serializer.Serialize(zipStream, newPackage);

            Console.WriteLine($"*** PackageBuilder: Create the spec file.");
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
        /// Deletes a file and ignores the failure, because it runs while another error is
        /// already on its way to the caller.
        /// </summary>
        /// <param name="path">The file to delete.</param>
        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Derives the name a plugin carries inside the package. The packed files and the spec
        /// inside the package must agree on it, or the package manager cannot find the plugin's
        /// assembly; a trailing separator (src/Plugin/) would otherwise yield an empty name.
        /// </summary>
        /// <param name="plugin">The plugin path as written in the spec.</param>
        /// <returns>The sanitized name of the plugin's folder.</returns>
        private static string PluginName(string plugin)
        {
            return SanitizeFileNameComponent(Path.GetFileName(plugin.TrimEnd('/', '\\')));
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
