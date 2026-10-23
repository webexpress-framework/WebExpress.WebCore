using System.IO;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebResource
{
    /// <summary>
    /// A binary resource that serves a file read from disk, delivering its bytes to the client.
    /// Access is guarded so concurrent requests can read the same file safely.
    /// </summary>
    public class ResourceFile : ResourceBinary
    {
        /// <summary>
        /// Gets the protection in case of concurrency.
        /// </summary>
        private object Guard { get; set; }

        /// <summary>
        /// Gets the root directory.
        /// </summary>
        public string RootDirectory { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="resourceContext">The resource context.</param>
        public ResourceFile(IResourceContext resourceContext)
            : base(resourceContext)
        {
            Guard = new object();
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public override IResponse Process(IRequest request)
        {
            lock (Guard)
            {
                var requestUri = request.Uri.ToString();
                var routePrefix = ResourceContext.Route.ToString();

                if (string.IsNullOrEmpty(requestUri) ||
                    routePrefix is null ||
                    !requestUri.StartsWith(routePrefix) ||
                    requestUri.Length < routePrefix.Length)
                {
                    return new ResponseNotFound();
                }

                var url = requestUri.Length == routePrefix.Length
                    ? string.Empty
                    : requestUri[routePrefix.Length..];

                var path = Path.GetFullPath(RootDirectory + url);

                if (!File.Exists(path))
                {
                    return new ResponseNotFound();
                }

                // derive the ETag from file metadata so an unchanged file can be answered with 304
                // without reading its content from disk.
                var eTag = ComputeETag(new FileInfo(path));
                if (IsNotModified(request, eTag))
                {
                    return CreateNotModifiedResponse(eTag);
                }

                Data = File.ReadAllBytes(path);

                var response = base.Process(request);
                response.Header.CacheControl = "public, max-age=31536000";

                // content type and download handling are resolved through the shared logic
                ApplyContentType(response, path);

                if (!string.IsNullOrEmpty(eTag))
                {
                    response.Header.AddCustomHeader("ETag", eTag);
                }

                request.HttpServerContext.Log?.Debug(I18N.Translate("webexpress.webcore:resource.file", request.RemoteEndPoint, request.Uri));

                return response;
            }
        }
    }
}
