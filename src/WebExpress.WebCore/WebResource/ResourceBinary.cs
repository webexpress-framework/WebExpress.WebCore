using System;
using System.IO;
using System.Security.Cryptography;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebResource
{
    /// <summary>
    /// Base class for a resource that returns raw bytes (such as an image or download) rather than a
    /// rendered page. Subclasses supply the <see cref="Data"/> to send to the client.
    /// </summary>
    public abstract class ResourceBinary : Resource
    {
        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="resourceContext">The resource context.</param>
        public ResourceBinary(IResourceContext resourceContext)
            : base(resourceContext)
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public override IResponse Process(IRequest request)
        {
            var response = new ResponseOK();
            response.Header.ContentLength = Data is not null ? Data.Length : 0;
            response.Header.ContentType = ContentType.Unknown.GetMimeType();

            response.Content = Data;

            return response;
        }

        /// <summary>
        /// Applies the content type for the given file to the response. The mapping is resolved through
        /// the shared <see cref="ContentTypeExtensions"/> so every resource uses the same single source
        /// of truth. Archives and executables are additionally offered as a download.
        /// </summary>
        /// <param name="response">The response to decorate.</param>
        /// <param name="fileName">The file name (or path) the content type is derived from.</param>
        protected void ApplyContentType(IResponse response, string fileName)
        {
            var contentType = ContentTypeExtensions.ToContentType(Path.GetExtension(fileName));
            response.Header.ContentType = contentType.GetMimeType();

            if (contentType == ContentType.Zip || contentType == ContentType.Exe)
            {
                response.Header.ContentDisposition = $"attachment; filename={Path.GetFileName(fileName)}; size={(Data?.LongLength ?? 0)}";
            }
        }

        /// <summary>
        /// Computes a content-based entity tag (ETag) for conditional requests.
        /// </summary>
        /// <param name="data">The payload.</param>
        /// <returns>A quoted ETag value, or null when there is no payload.</returns>
        protected static string ComputeETag(byte[] data)
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
        /// Computes an entity tag (ETag) from file metadata. This avoids reading the file content,
        /// so a conditional request can be answered without touching the disk.
        /// </summary>
        /// <param name="info">The file to derive the tag from.</param>
        /// <returns>A quoted ETag value, or null when the file does not exist.</returns>
        protected static string ComputeETag(FileInfo info)
        {
            if (info is null || !info.Exists)
            {
                return null;
            }

            // length and last-write time change together with the content for practical purposes
            return "\"" + info.Length.ToString("x") + "-" + info.LastWriteTimeUtc.Ticks.ToString("x") + "\"";
        }

        /// <summary>
        /// Determines whether the request already holds the current version of the resource and can be
        /// answered with 304 Not Modified.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="eTag">The current entity tag of the resource.</param>
        /// <returns>True when the client's If-None-Match matches the given tag; otherwise false.</returns>
        protected static bool IsNotModified(IRequest request, string eTag)
        {
            return !string.IsNullOrEmpty(eTag) && string.Equals(request?.Header?.IfNoneMatch, eTag, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds a 304 Not Modified response that re-advertises the cache headers.
        /// </summary>
        /// <param name="eTag">The current entity tag of the resource.</param>
        /// <returns>The 304 response.</returns>
        protected static IResponse CreateNotModifiedResponse(string eTag)
        {
            var response = new ResponseNotModified();
            response.Header.CacheControl = "public, max-age=31536000";
            response.Header.AddCustomHeader("ETag", eTag);

            return response;
        }
    }
}
