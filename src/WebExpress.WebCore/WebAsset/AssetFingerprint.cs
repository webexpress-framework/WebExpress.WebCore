using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// The fingerprints of the embedded files of a plugin: the hash of the content an address of
    /// a file carries, so a changed file gets a new address and every address can be cached for good.
    /// </summary>
    /// <remarks>
    /// A page links scripts and stylesheets, and a stylesheet addresses images and fonts - the
    /// icon set alone is close to two thousand drawings. A stylesheet names those files by their
    /// plain relative path, so it is delivered with each such path rewritten to the fingerprinted
    /// name of the file, and its own fingerprint is taken from the rewritten content: a changed
    /// drawing changes the stylesheet, and with it the address the page links, without anyone
    /// touching the stylesheet. The paths stay relative, so the content is the same in every
    /// application the plugin is mounted in.
    /// The fingerprints are kept per assembly rather than per hub: they follow from the content
    /// alone, and hashing the icon set again for every hub would dominate its start.
    /// </remarks>
    internal static class AssetFingerprint
    {
        // the files a page or a stylesheet addresses; other files keep only their plain address
        private static readonly HashSet<string> FingerprintedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".js", ".css",
            ".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".ico", ".bmp",
            ".woff", ".woff2", ".ttf", ".otf", ".eot"
        };

        // a url() of a stylesheet, quoted or not; the quote is kept as it was written
        private static readonly Regex UrlPattern = new
        (
            @"url\(\s*(?<quote>[""']?)(?<url>[^""')]*?)\k<quote>\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
        );

        private static readonly ConditionalWeakTable<Assembly, Catalog> Catalogs = [];

        /// <summary>
        /// Returns the path of an embedded file below the asset directory of its plugin.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly the resource is embedded in.</param>
        /// <param name="resource">The embedded resource name.</param>
        /// <returns>The path, or null when the resource is no asset.</returns>
        internal static string GetId(string assemblyName, string resource)
        {
            var prefix = assemblyName + ".Assets.";

            return resource is not null && resource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? resource[prefix.Length..].Replace('\\', '/')
                : null;
        }

        /// <summary>
        /// Returns the fingerprint of an embedded file, computed once per assembly.
        /// </summary>
        /// <param name="assembly">The assembly the resource is embedded in.</param>
        /// <param name="resource">The embedded resource name.</param>
        /// <returns>The fingerprint, or null when the file keeps only its plain address.</returns>
        internal static string GetVersion(Assembly assembly, string resource)
        {
            if (assembly is null || resource is null || !FingerprintedExtensions.Contains(Path.GetExtension(resource)))
            {
                return null;
            }

            return GetCatalog(assembly).Versions.GetOrAdd(resource, x => Asset.ComputeVersion(ReadContent(assembly, x)));
        }

        /// <summary>
        /// Returns the content of an embedded file as it is delivered: a stylesheet with the paths
        /// of the files it addresses rewritten to their fingerprinted names, any other file as it
        /// is embedded.
        /// </summary>
        /// <param name="assembly">The assembly the resource is embedded in.</param>
        /// <param name="resource">The embedded resource name.</param>
        /// <returns>The content.</returns>
        internal static byte[] ReadContent(Assembly assembly, string resource)
        {
            var data = Asset.ReadResource(assembly, resource);

            if (!string.Equals(Path.GetExtension(resource), ".css", StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }

            var id = GetId(assembly.GetName().Name, resource);

            return id is null ? data : Rewrite(assembly, id, data);
        }

        /// <summary>
        /// Inserts the fingerprint before the extension of a file path.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="version">The fingerprint.</param>
        /// <returns>The path with the fingerprint in the file name.</returns>
        internal static string InsertVersion(string path, string version)
        {
            var extension = Path.GetExtension(path);

            return $"{path[..^extension.Length]}.{version}{extension}";
        }

        /// <summary>
        /// Rewrites every relative url() of a stylesheet that names a fingerprinted file of the same
        /// plugin to the fingerprinted name of that file. Absolute, external and data urls, and a
        /// path that names no embedded file, are left as they are.
        /// </summary>
        /// <param name="assembly">The assembly the stylesheet is embedded in.</param>
        /// <param name="id">The path of the stylesheet below the asset directory.</param>
        /// <param name="data">The content of the stylesheet.</param>
        /// <returns>The rewritten content, or the given one when nothing was rewritten.</returns>
        internal static byte[] Rewrite(Assembly assembly, string id, byte[] data)
        {
            var catalog = GetCatalog(assembly);
            var directory = id[..(id.LastIndexOf('/') + 1)];
            var rewritten = false;

            var text = UrlPattern.Replace(Encoding.UTF8.GetString(data), match =>
            {
                var url = match.Groups["url"].Value.Trim();

                // a query or a fragment selects within the file and stays with it
                var end = url.IndexOfAny(['?', '#']);
                var path = end < 0 ? url : url[..end];
                var suffix = end < 0 ? string.Empty : url[end..];

                if (!IsRelative(path) || string.Equals(Path.GetExtension(path), ".css", StringComparison.OrdinalIgnoreCase))
                {
                    return match.Value;
                }

                var target = Resolve(directory, path);

                if (target is null || !catalog.Resources.TryGetValue(target, out var resource) ||
                    GetVersion(assembly, resource) is not { } version)
                {
                    return match.Value;
                }

                rewritten = true;
                var quote = match.Groups["quote"].Value;

                return $"url({quote}{InsertVersion(path, version)}{suffix}{quote})";
            });

            return rewritten ? Encoding.UTF8.GetBytes(text) : data;
        }

        /// <summary>
        /// Resolves a relative path against the directory of the file it is written in.
        /// </summary>
        /// <param name="directory">The directory of the file, with a trailing separator or empty.</param>
        /// <param name="path">The relative path.</param>
        /// <returns>The resolved path below the asset directory, or null when it leaves it.</returns>
        internal static string Resolve(string directory, string path)
        {
            var segments = new List<string>();

            foreach (var segment in (directory + path).Split('/'))
            {
                if (segment is "" or ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count == 0)
                    {
                        return null;
                    }

                    segments.RemoveAt(segments.Count - 1);

                    continue;
                }

                segments.Add(segment);
            }

            return segments.Count == 0 ? null : string.Join('/', segments);
        }

        /// <summary>
        /// Determines whether a url is a path relative to the stylesheet.
        /// </summary>
        /// <param name="path">The url without query and fragment.</param>
        /// <returns>True for a relative path.</returns>
        private static bool IsRelative(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] is '/' or '\\')
            {
                return false;
            }

            // a scheme (data:, https:) ends before the first separator
            var colon = path.IndexOf(':');
            var slash = path.IndexOf('/');

            return colon < 0 || (slash >= 0 && slash < colon);
        }

        /// <summary>
        /// Returns the catalog of an assembly, created on first use.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The catalog.</returns>
        private static Catalog GetCatalog(Assembly assembly)
        {
            return Catalogs.GetValue(assembly, x => new Catalog(x));
        }

        /// <summary>
        /// The embedded files of an assembly by their path, and the fingerprints computed so far.
        /// </summary>
        private sealed class Catalog
        {
            /// <summary>
            /// Returns the embedded resource names by the path of the file below the asset directory.
            /// </summary>
            public Dictionary<string, string> Resources { get; }

            /// <summary>
            /// Returns the fingerprints by embedded resource name.
            /// </summary>
            public ConcurrentDictionary<string, string> Versions { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// Initializes a new instance of the class.
            /// </summary>
            /// <param name="assembly">The assembly.</param>
            public Catalog(Assembly assembly)
            {
                var assemblyName = assembly.GetName().Name;

                Resources = assembly.GetManifestResourceNames()
                    .Select(x => (Id: GetId(assemblyName, x), Resource: x))
                    .Where(x => x.Id is not null)
                    .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Resource, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
