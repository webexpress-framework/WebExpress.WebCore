using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// Delivery of a resource embedded in the assembly.
    /// </summary>
    public class Asset : IAsset
    {
        // the address of a plain asset carries no version, so a copy reused without asking would keep
        // an outdated script after every deployment; the entity tag makes revalidation a cheap 304
        private const string CacheControlRevalidate = "no-cache";

        // the address of a versioned asset carries a hash of its content, so a changed file is a new
        // address and a copy of the old one never goes stale
        private const string CacheControlImmutable = "public, max-age=31536000, immutable";

        // the number of hex digits of the content hash that make up the version in an address
        private const int VersionLength = 12;

        private readonly string _cacheControl;
        private readonly IComponentHub _componentHub;
        private readonly IAssetContext _assetContext;
        private readonly IHttpServerContext _httpServerContext;
        private readonly string _embeddedResource;
        private readonly ContentType _contentType;

        /// <summary>
        /// The payload and its entity tag, read on the first request.
        /// </summary>
        /// <remarks>
        /// A plugin embeds thousands of assets and every hub registers all of them, while a
        /// client asks for few; reading and hashing each one up front made the registration the
        /// bulk of a hub's start. Null once the asset is disposed.
        /// </remarks>
        private Lazy<AssetPayload> _payload;

        /// <summary>
        /// Gets the root directory.
        /// </summary>
        public string AssetDirectory { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="assetContext">The asset context.</param>
        /// <param name="httpServerContext">The server context.</param>
        /// <param name="embeddedResource">The embedded resource name.</param>
        public Asset(IComponentHub componentHub, IAssetContext assetContext, IHttpServerContext httpServerContext, string embeddedResource)
        {
            _componentHub = componentHub;
            _assetContext = assetContext;
            _httpServerContext = httpServerContext;
            _embeddedResource = embeddedResource;

            var assembly = _assetContext.PluginContext.Assembly;

            _cacheControl = ResolveCacheControl(_assetContext);

            // an asset is immutable embedded content, so its content type and entity tag are
            // resolved once instead of on every request.
            _contentType = ResolveContentType(_assetContext);
            // a stylesheet is delivered with the files it addresses rewritten to their
            // fingerprinted names, the content its own fingerprint is taken from
            _payload = new(() => new AssetPayload(AssetFingerprint.ReadContent(assembly, _embeddedResource), _contentType));
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public IResponse Process(IRequest request)
        {
            if (_payload is not { } payload)
            {
                return new ResponseNotFound();
            }

            var response = payload.Value.Respond(request, _contentType, _cacheControl, _assetContext.EndpointId?.ToString());

            if (response is ResponseOK)
            {
                _httpServerContext?.Log?.Debug(I18N.Translate
                (
                    "webexpress.webcore:asset.file",
                    request.RemoteEndPoint, request.Uri
                ));
            }

            return response;
        }

        /// <summary>
        /// Returns the content of the asset for a bundle. The payload is not created for it: in a
        /// bundled application the file itself is rarely asked for, and keeping it next to the
        /// bundle would hold every script twice.
        /// </summary>
        /// <returns>The content, empty once the asset is disposed.</returns>
        internal byte[] ReadContent()
        {
            if (_payload is not { } payload)
            {
                return [];
            }

            return payload.IsValueCreated
                ? payload.Value.Data
                : AssetFingerprint.ReadContent(_assetContext.PluginContext.Assembly, _embeddedResource);
        }

        /// <summary>
        /// Returns the cache policy of the address an asset is served from.
        /// </summary>
        /// <param name="assetContext">The asset context.</param>
        /// <returns>The value of the Cache-Control header.</returns>
        internal static string ResolveCacheControl(IAssetContext assetContext)
        {
            return assetContext is AssetContext { Version: not null }
                ? CacheControlImmutable
                : CacheControlRevalidate;
        }

        /// <summary>
        /// Returns the content type of an asset, which follows from the extension of its address.
        /// </summary>
        /// <param name="assetContext">The asset context.</param>
        /// <returns>The content type.</returns>
        internal static ContentType ResolveContentType(IAssetContext assetContext)
        {
            return ContentTypeExtensions.ToContentType(Path.GetExtension(assetContext?.EndpointId?.ToString()));
        }

        /// <summary>
        /// Reads the data of an embedded resource.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="embeddedResource">The embedded resource name.</param>
        /// <returns>The data.</returns>
        internal static byte[] ReadResource(Assembly assembly, string embeddedResource)
        {
            if (assembly is null || embeddedResource is null)
            {
                return [];
            }

            using var stream = assembly.GetManifestResourceStream(embeddedResource);

            if (stream is null)
            {
                return [];
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Parses a Range header of the form <c>bytes=start-end</c>, <c>bytes=start-</c> or
        /// <c>bytes=-suffix</c>. Several ranges in one header are not supported; like a header
        /// that cannot be parsed they are answered with the whole file, which the specification
        /// permits.
        /// </summary>
        /// <param name="header">The value of the Range header.</param>
        /// <param name="length">The length of the file.</param>
        /// <returns>
        /// The first and the last byte of the range, (-1, -1) for a range outside of the file,
        /// or null when the whole file is to be sent.
        /// </returns>
        internal static (long Start, long End)? ParseRange(string header, long length)
        {
            const string unit = "bytes=";

            if (string.IsNullOrWhiteSpace(header) || !header.StartsWith(unit, StringComparison.OrdinalIgnoreCase) || header.Contains(','))
            {
                return null;
            }

            var spec = header[unit.Length..].Trim();
            var dash = spec.IndexOf('-');

            if (dash < 0)
            {
                return null;
            }

            var first = spec[..dash].Trim();
            var last = spec[(dash + 1)..].Trim();

            if (first.Length == 0)
            {
                // a suffix range names the number of bytes at the end of the file
                if (!long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var suffix))
                {
                    return null;
                }

                if (suffix == 0 || length == 0)
                {
                    return (-1, -1);
                }

                return (Math.Max(0, length - suffix), length - 1);
            }

            if (!long.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var start))
            {
                return null;
            }

            var end = length - 1;

            if (last.Length > 0)
            {
                if (!long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out end) || end < start)
                {
                    return null;
                }
            }

            if (start >= length)
            {
                return (-1, -1);
            }

            return (start, Math.Min(end, length - 1));
        }

        /// <summary>
        /// Computes a content-based entity tag (ETag) for conditional requests.
        /// </summary>
        /// <param name="data">The asset payload.</param>
        /// <returns>A quoted ETag value, or null when there is no payload.</returns>
        internal static string ComputeETag(byte[] data)
        {
            var hash = ComputeHash(data);

            return hash is null ? null : "\"" + hash + "\"";
        }

        /// <summary>
        /// Computes the content hash of an asset.
        /// </summary>
        /// <param name="data">The asset payload.</param>
        /// <returns>The hash as lower case hex digits, or null when there is no payload.</returns>
        private static string ComputeHash(byte[] data)
        {
            if (data is null || data.Length == 0)
            {
                return null;
            }

            // a content hash is used so the value changes if and only if the payload changes; SHA-1 is
            // chosen for speed because the value is a cache validator, not a security token.
            return Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
        }

        /// <summary>
        /// Computes the version an address of the asset carries, so that a changed file is
        /// served from a new address.
        /// </summary>
        /// <param name="data">The asset payload.</param>
        /// <returns>The version, or null when there is no payload.</returns>
        internal static string ComputeVersion(byte[] data)
        {
            return ComputeHash(data)?[..VersionLength];
        }

        /// <summary>
        /// Performs application-specific tasks related to sharing, returning, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _payload = null;
        }
    }
}
