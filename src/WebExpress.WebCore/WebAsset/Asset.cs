using System;
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
        private readonly IComponentHub _componentHub;
        private readonly IAssetContext _assetContext;
        private readonly IHttpServerContext _httpServerContext;
        private readonly string _embeddedResource;
        private readonly ContentType _contentType;
        private readonly string _eTag;
        private byte[] _data;

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
            _data = GetData(assembly);

            // an asset is immutable embedded content, so its content type and entity tag can be
            // resolved once at construction time instead of on every request.
            _contentType = ContentTypeExtensions.ToContentType(Path.GetExtension(_assetContext.EndpointId?.ToString()));
            _eTag = ComputeETag(_data);
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public IResponse Process(IRequest request)
        {
            if (_data is null)
            {
                return new ResponseNotFound();
            }

            // conditional request: when the client already holds the current version, skip the body
            // and answer with 304 Not Modified so the cached copy is reused.
            if (!string.IsNullOrEmpty(_eTag) && string.Equals(request?.Header?.IfNoneMatch, _eTag, StringComparison.Ordinal))
            {
                var notModified = new ResponseNotModified();
                notModified.Header.CacheControl = "public, max-age=31536000";
                notModified.Header.AddCustomHeader("ETag", _eTag);

                return notModified;
            }

            var response = new ResponseOK();
            response.Header.CacheControl = "public, max-age=31536000";
            response.Header.ContentLength = _data.Length;
            response.Header.ContentType = _contentType.GetMimeType();
            response.Content = _data;

            if (!string.IsNullOrEmpty(_eTag))
            {
                response.Header.AddCustomHeader("ETag", _eTag);
            }

            // archives are offered as a download rather than rendered inline
            if (_contentType == ContentType.Zip)
            {
                response.Header.ContentDisposition = $"attachment; filename={_assetContext.EndpointId}; size={_data.LongLength}";
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
            _data = null;
        }
    }
}
