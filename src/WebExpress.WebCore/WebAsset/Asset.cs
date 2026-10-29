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
        // the address of an asset carries no version, so a copy reused without asking would keep
        // an outdated script after every deployment; the entity tag makes revalidation a cheap 304
        private const string CacheControl = "no-cache";

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
        private Lazy<(byte[] Data, string ETag)> _payload;

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

            // an asset is immutable embedded content, so its content type and entity tag are
            // resolved once instead of on every request.
            _contentType = ContentTypeExtensions.ToContentType(Path.GetExtension(_assetContext.EndpointId?.ToString()));
            _payload = new(() =>
            {
                var data = GetData(assembly);

                return (data, ComputeETag(data));
            });
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

            var (data, eTag) = payload.Value;

            // conditional request: when the client already holds the current version, skip the body
            // and answer with 304 Not Modified so the cached copy is reused.
            if (!string.IsNullOrEmpty(eTag) && string.Equals(request?.Header?.IfNoneMatch, eTag, StringComparison.Ordinal))
            {
                var notModified = new ResponseNotModified();
                notModified.Header.CacheControl = CacheControl;
                notModified.Header.AddCustomHeader("ETag", eTag);

                return notModified;
            }

            // a range of an outdated version would be joined to parts of the current one, so a
            // mismatching If-Range is answered with the whole file
            var rangeHeader = request?.Header?.Range;
            var ifRange = request?.Header?.IfRange;

            if (request?.Method == RequestMethod.GET && !string.IsNullOrWhiteSpace(rangeHeader) &&
                (string.IsNullOrWhiteSpace(ifRange) || string.Equals(ifRange, eTag, StringComparison.Ordinal)))
            {
                var range = ParseRange(rangeHeader, data.LongLength);

                if (range == (-1, -1))
                {
                    var notSatisfiable = new ResponseRangeNotSatisfiable();
                    notSatisfiable.Header.AddCustomHeader("Content-Range", $"bytes */{data.LongLength}");

                    return notSatisfiable;
                }

                if (range is (var start, var end))
                {
                    var partial = new ResponsePartialContent();
                    partial.Header.CacheControl = CacheControl;
                    partial.Header.ContentLength = (int)(end - start + 1);
                    partial.Header.ContentType = _contentType.GetMimeType();
                    partial.Header.AddCustomHeader("Accept-Ranges", "bytes");
                    partial.Header.AddCustomHeader("Content-Range", $"bytes {start}-{end}/{data.LongLength}");
                    partial.Content = data[(int)start..(int)(end + 1)];

                    if (!string.IsNullOrEmpty(eTag))
                    {
                        partial.Header.AddCustomHeader("ETag", eTag);
                    }

                    return partial;
                }
            }

            var response = new ResponseOK();
            response.Header.CacheControl = CacheControl;
            response.Header.ContentLength = data.Length;
            response.Header.ContentType = _contentType.GetMimeType();
            response.Content = data;

            // tells media players they may seek by asking for parts; safari plays no video without it
            response.Header.AddCustomHeader("Accept-Ranges", "bytes");

            if (!string.IsNullOrEmpty(eTag))
            {
                response.Header.AddCustomHeader("ETag", eTag);
            }

            // archives are offered as a download rather than rendered inline
            if (_contentType == ContentType.Zip)
            {
                response.Header.ContentDisposition = $"attachment; filename={_assetContext.EndpointId}; size={data.LongLength}";
            }

            _httpServerContext?.Log?.Debug(I18N.Translate
            (
                "webexpress.webcore:asset.file",
                request.RemoteEndPoint, request.Uri
            ));

            return response;
        }

        /// <summary>
        /// Reads the data of a specified resource.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The data.</returns>
        private byte[] GetData(Assembly assembly)
        {
            if (assembly is null || _embeddedResource is null)
            {
                return [];
            }

            using var stream = assembly.GetManifestResourceStream(_embeddedResource);

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
        private static string ComputeETag(byte[] data)
        {
            if (data is null || data.Length == 0)
            {
                return null;
            }

            // a content hash is used so the tag changes if and only if the payload changes; SHA-1 is
            // chosen for speed because the value is a cache validator, not a security token.
            return "\"" + Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant() + "\"";
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
