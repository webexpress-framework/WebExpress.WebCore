using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebResource
{
    /// <summary>
    /// Delivery of a resource embedded in the assembly.
    /// </summary>
    public class ResourceAsset : ResourceBinary
    {
        /// <summary>
        /// Gets the protection against concurrency.
        /// </summary>
        private object Gard { get; set; }

        /// <summary>
        /// The entity tags of the delivered files, keyed by resource file name. Embedded resources
        /// are immutable for the lifetime of the process, so each content hash is computed once
        /// instead of on every request.
        /// </summary>
        private readonly Dictionary<string, string> _eTags = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the root directory.
        /// </summary>
        public string AssetDirectory { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="resourceContext">The resource context.</param>
        public ResourceAsset(IResourceContext resourceContext)
            : base(resourceContext)
        {
            Gard = new object();
            AssetDirectory = ResourceContext.PluginContext.Assembly.GetName().Name;
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public override IResponse Process(IRequest request)
        {
            lock (Gard)
            {
                var assembly = ResourceContext.PluginContext.Assembly;
                var resources = assembly.GetManifestResourceNames().Where(x => x.StartsWith(AssetDirectory, StringComparison.OrdinalIgnoreCase));
                var url = request.Uri.ToString();
                var fileName = Path.GetFileName(url);
                var file = string.Join('.', AssetDirectory.Trim('.'), "assets", url.Replace("/", ".").Trim('.'));

                Data = GetData(file, assembly, resources.ToList());

                if (Data is null)
                {
                    return new ResponseNotFound();
                }

                // conditional request: serve 304 when the client already holds the current version
                if (!_eTags.TryGetValue(file, out var eTag))
                {
                    eTag = ComputeETag(Data);
                    _eTags[file] = eTag;
                }

                if (IsNotModified(request, eTag))
                {
                    return CreateNotModifiedResponse(eTag);
                }

                var response = base.Process(request);
                response.Header.CacheControl = "public, max-age=31536000";

                // content type and download handling are resolved through the shared logic
                ApplyContentType(response, fileName);

                if (!string.IsNullOrEmpty(eTag))
                {
                    response.Header.AddCustomHeader("ETag", eTag);
                }

                request.HttpServerContext.Log?.Debug(I18N.Translate
                    (
                        "webexpress.webcore:resource.file",
                        request.RemoteEndPoint, request.Uri
                    ));

                return response;
            }
        }

        /// <summary>
        /// Reads the data of a specified resource.
        /// </summary>
        /// <param name="file">The name of the resource file to read.</param>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <param name="resources">A collection of resource names available in the assembly.</param>
        /// <returns>A byte array containing the resource data, or null if the resource is not found.</returns>
        private static byte[] GetData(string file, Assembly assembly, IEnumerable<string> resources)
        {
            var item = resources.Where(x => x.Equals(file, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (item is null)
            {
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(item);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Performs application-specific tasks related to sharing, returning, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {

        }
    }
}
