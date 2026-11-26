using System;
using System.Collections.Generic;
using System.IO;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// Delivery of the files of an include joined into one, so a page loads them with one request
    /// instead of one each. A page links several hundred files, and every request costs its
    /// round trip and its share of the connection limit before the page can run.
    /// </summary>
    internal sealed class AssetBundle : IAsset
    {
        private readonly IAssetContext _assetContext;
        private readonly IHttpServerContext _httpServerContext;
        private readonly string _cacheControl;
        private readonly ContentType _contentType;

        /// <summary>
        /// The joined content. Shared by the plain and the versioned address of the bundle, so the
        /// content and each compressed representation of it are held once. Null once disposed.
        /// </summary>
        private Lazy<AssetPayload> _payload;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="assetContext">The asset context.</param>
        /// <param name="httpServerContext">The server context.</param>
        /// <param name="payload">The joined content.</param>
        public AssetBundle(IAssetContext assetContext, IHttpServerContext httpServerContext, Lazy<AssetPayload> payload)
        {
            _assetContext = assetContext;
            _httpServerContext = httpServerContext;
            _cacheControl = Asset.ResolveCacheControl(assetContext);
            _contentType = Asset.ResolveContentType(assetContext);
            _payload = payload;
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
        /// Joins the contents of several files into the content of one bundle, in the given order.
        /// </summary>
        /// <remarks>
        /// A file is a unit of its own when it is linked, which the joined files are not:
        /// <list type="bullet">
        /// <item>a byte order mark is part of the first rule of a stylesheet that follows another,
        /// which makes the browser drop that rule, so it is removed from every file;</item>
        /// <item>a script that ends in a // comment or relies on automatic semicolon insertion
        /// would run into the next one, so scripts are separated by a line break and a semicolon.</item>
        /// </list>
        /// </remarks>
        /// <param name="contentType">The content type of the files.</param>
        /// <param name="contents">The contents of the files, in the order they are linked in.</param>
        /// <returns>The content of the bundle.</returns>
        internal static byte[] Join(ContentType contentType, IEnumerable<byte[]> contents)
        {
            var separator = contentType == ContentType.Js ? "\n;\n"u8.ToArray() : "\n"u8.ToArray();
            using var output = new MemoryStream();
            var first = true;

            foreach (var content in contents)
            {
                if (!first)
                {
                    output.Write(separator);
                }

                output.Write(WithoutByteOrderMark(content ?? []));
                first = false;
            }

            return output.ToArray();
        }

        /// <summary>
        /// Returns the content of a file without a leading utf-8 byte order mark.
        /// </summary>
        /// <param name="content">The content of the file.</param>
        /// <returns>The content without the mark.</returns>
        private static ReadOnlySpan<byte> WithoutByteOrderMark(byte[] content)
        {
            ReadOnlySpan<byte> span = content;

            return span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? span[3..] : span;
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
