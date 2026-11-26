using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// The content of an asset together with the compressed representations it is offered in and
    /// the entity tag of each, and the answer to a request for it.
    /// </summary>
    /// <remarks>
    /// A page links several hundred scripts and stylesheets, which compress to a fraction of their
    /// size. Each compressed representation is produced on the first request that accepts it and
    /// kept for the lifetime of the asset: the content never changes, so compressing it per request
    /// would repeat the same work, and compressing every file up front would make the start pay for
    /// files no client asks for. Shared by an embedded file and a bundle, so both are answered alike.
    /// </remarks>
    internal sealed class AssetPayload
    {
        /// <summary>
        /// The token of the brotli content coding.
        /// </summary>
        public const string Brotli = "br";

        /// <summary>
        /// The token of the gzip content coding.
        /// </summary>
        public const string Gzip = "gzip";

        // the highest quality is affordable because each representation is compressed once
        private const int BrotliQuality = 11;

        // the window every browser decodes; a larger one gains little on files of this size
        private const int BrotliWindow = 22;

        // the highest brotli quality takes about 2 ms per KB: nothing for a single file, but
        // seconds for a bundle of the framework's scripts, which no request should wait for
        private const int BackgroundCompressionThreshold = 32 * 1024;

        // the formats that are text or an uncompressed binary; images, archives, media and woff
        // fonts are compressed already and would only grow
        private static readonly HashSet<ContentType> CompressibleTypes =
        [
            ContentType.Txt, ContentType.Css, ContentType.Js, ContentType.Xml, ContentType.Htm,
            ContentType.Html, ContentType.Json, ContentType.Csv, ContentType.Svg, ContentType.Vtt,
            ContentType.Wasm, ContentType.Ttf, ContentType.Otf, ContentType.Eot, ContentType.Ico,
            ContentType.Bmp
        ];

        private readonly Lazy<Task<byte[]>> _brotli;
        private readonly Lazy<byte[]> _gzip;

        /// <summary>
        /// Returns the uncompressed content.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Returns the entity tag of the uncompressed content, or null without content.
        /// </summary>
        public string ETag { get; }

        /// <summary>
        /// Returns whether the content is offered compressed, which makes every answer depend
        /// on the Accept-Encoding of the request.
        /// </summary>
        public bool Compressible { get; }

        /// <summary>
        /// Returns the brotli representation, started by the first request that accepts it, or
        /// null when it does not save anything.
        /// </summary>
        /// <remarks>
        /// Large content is compressed in the background: until it is done, a client that also
        /// accepts gzip receives that, which is ready in a fraction of the time, and the plain
        /// content otherwise.
        /// </remarks>
        internal Task<byte[]> BrotliCompression => _brotli.Value;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="data">The uncompressed content.</param>
        /// <param name="contentType">The content type, which decides whether compressing pays off.</param>
        public AssetPayload(byte[] data, ContentType contentType)
        {
            Data = data ?? [];
            ETag = Asset.ComputeETag(Data);
            Compressible = Data.Length > 0 && CompressibleTypes.Contains(contentType);

            _brotli = new(() => Data.Length > BackgroundCompressionThreshold
                ? Task.Run(() => Smaller(CompressBrotli(Data)))
                : Task.FromResult(Smaller(CompressBrotli(Data))));
            _gzip = new(() => Smaller(CompressGzip(Data)));
        }

        /// <summary>
        /// Answers a request for the content: with 304 when the client holds the representation
        /// it would receive, with a part of the uncompressed content for a range, and otherwise
        /// with the best representation the client accepts.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="contentType">The content type.</param>
        /// <param name="cacheControl">The cache policy of the address the content is served from.</param>
        /// <param name="downloadName">The file name an archive is offered for download under.</param>
        /// <returns>The response.</returns>
        public IResponse Respond(IRequest request, ContentType contentType, string cacheControl, string downloadName)
        {
            var header = request?.Header;

            // a range of an outdated version would be joined to parts of the current one, so a
            // mismatching If-Range is answered with the whole file
            var ifRange = header?.IfRange;
            var range = request?.Method == RequestMethod.GET && !string.IsNullOrWhiteSpace(header?.Range) &&
                (string.IsNullOrWhiteSpace(ifRange) || string.Equals(ifRange, ETag, StringComparison.Ordinal))
                    ? Asset.ParseRange(header.Range, Data.LongLength)
                    : null;

            // a range counts the bytes of the file itself, so it is always cut from those
            var (data, eTag, encoding) = range is null
                ? Select(header?.AcceptEncoding)
                : (Data, ETag, null);

            // conditional request: when the client already holds the current version, skip the body
            // and answer with 304 Not Modified so the cached copy is reused.
            if (!string.IsNullOrEmpty(eTag) && string.Equals(header?.IfNoneMatch, eTag, StringComparison.Ordinal))
            {
                var notModified = new ResponseNotModified();
                notModified.Header.CacheControl = cacheControl;
                notModified.Header.AddCustomHeader("ETag", eTag);
                AddVary(notModified);

                return notModified;
            }

            if (range == (-1, -1))
            {
                var notSatisfiable = new ResponseRangeNotSatisfiable();
                notSatisfiable.Header.AddCustomHeader("Content-Range", $"bytes */{Data.LongLength}");

                return notSatisfiable;
            }

            if (range is (var start, var end))
            {
                var partial = new ResponsePartialContent();
                partial.Header.CacheControl = cacheControl;
                partial.Header.ContentLength = (int)(end - start + 1);
                partial.Header.ContentType = contentType.GetMimeType();
                partial.Header.AddCustomHeader("Accept-Ranges", "bytes");
                partial.Header.AddCustomHeader("Content-Range", $"bytes {start}-{end}/{Data.LongLength}");
                partial.Content = Data[(int)start..(int)(end + 1)];
                AddVary(partial);

                if (!string.IsNullOrEmpty(eTag))
                {
                    partial.Header.AddCustomHeader("ETag", eTag);
                }

                return partial;
            }

            var response = new ResponseOK();
            response.Header.CacheControl = cacheControl;
            response.Header.ContentLength = data.Length;
            response.Header.ContentType = contentType.GetMimeType();
            response.Content = data;
            AddVary(response);

            // tells media players they may seek by asking for parts; safari plays no video without it
            response.Header.AddCustomHeader("Accept-Ranges", "bytes");

            if (encoding is not null)
            {
                response.Header.AddCustomHeader("Content-Encoding", encoding);
            }

            if (!string.IsNullOrEmpty(eTag))
            {
                response.Header.AddCustomHeader("ETag", eTag);
            }

            // archives are offered as a download rather than rendered inline
            if (contentType == ContentType.Zip)
            {
                response.Header.ContentDisposition = $"attachment; filename={downloadName}; size={data.LongLength}";
            }

            return response;
        }

        /// <summary>
        /// Returns the representation of the content for a client, together with its entity tag
        /// and content coding.
        /// </summary>
        /// <remarks>
        /// Each representation carries an entity tag of its own: a strong tag names exactly one
        /// sequence of bytes, and a cache that revalidates a brotli copy with the tag of the plain
        /// file would be told the bytes it holds are current when they are not the ones it asked for.
        /// </remarks>
        /// <param name="acceptEncoding">The Accept-Encoding header of the request.</param>
        /// <returns>The content, its entity tag and its content coding, null for the uncompressed content.</returns>
        public (byte[] Data, string ETag, string Encoding) Select(string acceptEncoding)
        {
            if (!Compressible)
            {
                return (Data, ETag, null);
            }

            foreach (var encoding in Negotiate(acceptEncoding))
            {
                var compressed = encoding == Brotli
                    ? (_brotli.Value is { IsCompletedSuccessfully: true } brotli ? brotli.Result : null)
                    : _gzip.Value;

                if (compressed is not null)
                {
                    return (compressed, ETag.Insert(ETag.Length - 1, encoding == Brotli ? "-br" : "-gz"), encoding);
                }
            }

            return (Data, ETag, null);
        }

        /// <summary>
        /// Returns the content codings the server offers that a client accepts, the preferred one
        /// first. A coding the header does not name is acceptable only through a wildcard, and one
        /// weighted with zero is refused. On equal weight brotli comes first, as it compresses
        /// scripts and stylesheets to noticeably less than gzip.
        /// </summary>
        /// <param name="acceptEncoding">The Accept-Encoding header of the request.</param>
        /// <returns>The accepted codings, preferred first; empty when only the plain content is acceptable.</returns>
        internal static IReadOnlyList<string> Negotiate(string acceptEncoding)
        {
            if (string.IsNullOrWhiteSpace(acceptEncoding))
            {
                return [];
            }

            double? brotli = null;
            double? gzip = null;
            double? any = null;

            foreach (var part in acceptEncoding.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var segments = part.Split(';', StringSplitOptions.TrimEntries);
                var weight = 1d;

                foreach (var parameter in segments.Skip(1))
                {
                    if (parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(parameter[2..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                    {
                        weight = value;
                    }
                }

                switch (segments[0].ToLowerInvariant())
                {
                    case Brotli:
                        brotli = weight;
                        break;
                    case Gzip:
                    case "x-gzip":
                        gzip = weight;
                        break;
                    case "*":
                        any = weight;
                        break;
                }
            }

            return [.. new[] { (Encoding: Brotli, Weight: brotli ?? any ?? 0), (Encoding: Gzip, Weight: gzip ?? any ?? 0) }
                .Where(x => x.Weight > 0)
                .OrderByDescending(x => x.Weight)
                .Select(x => x.Encoding)];
        }

        /// <summary>
        /// Tells caches that the answer depends on the codings the client accepts, so a copy
        /// compressed for one client is never handed to another that cannot decode it.
        /// </summary>
        /// <param name="response">The response.</param>
        private void AddVary(IResponse response)
        {
            if (Compressible)
            {
                response.Header.AddCustomHeader("Vary", "Accept-Encoding");
            }
        }

        /// <summary>
        /// Keeps a compressed representation only when it is smaller than the content itself.
        /// </summary>
        /// <param name="compressed">The compressed content.</param>
        /// <returns>The compressed content, or null when it does not save anything.</returns>
        private byte[] Smaller(byte[] compressed)
        {
            return compressed is not null && compressed.Length < Data.Length ? compressed : null;
        }

        /// <summary>
        /// Compresses data with brotli.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>The compressed data, or null when it cannot be compressed.</returns>
        private static byte[] CompressBrotli(byte[] data)
        {
            var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(data.Length)];

            return BrotliEncoder.TryCompress(data, buffer, out var written, BrotliQuality, BrotliWindow)
                ? buffer[..written]
                : null;
        }

        /// <summary>
        /// Compresses data with gzip.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>The compressed data.</returns>
        private static byte[] CompressGzip(byte[] data)
        {
            using var output = new MemoryStream();

            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(data);
            }

            return output.ToArray();
        }
    }
}
