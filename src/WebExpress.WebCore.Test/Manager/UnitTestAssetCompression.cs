using System.IO.Compression;
using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSitemap;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that an asset is answered in the best compressed representation the client accepts,
    /// with an entity tag of its own and without touching the cache policy of its address.
    /// </summary>
    public class UnitTestAssetCompression
    {
        private const string Uri = "http://localhost:8080/server/appa/assets/css/mycss.css";

        /// <summary>
        /// Tests the negotiation of the content coding.
        /// </summary>
        [Theory]
        [InlineData("gzip, deflate, br, zstd", "br;gzip")]
        [InlineData("gzip", "gzip")]
        [InlineData("x-gzip", "gzip")]
        [InlineData("BR", "br")]
        [InlineData("br;q=0, gzip", "gzip")]
        [InlineData("gzip;q=1, br;q=0.5", "gzip;br")]
        [InlineData("*", "br;gzip")]
        [InlineData("*;q=0", "")]
        [InlineData("gzip, *;q=0.2", "gzip;br")]
        [InlineData("identity", "")]
        [InlineData("deflate, zstd", "")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Negotiate(string acceptEncoding, string expected)
        {
            // act
            var encodings = AssetPayload.Negotiate(acceptEncoding);

            // validation
            Assert.Equal(expected, string.Join(";", encodings));
        }

        /// <summary>
        /// Tests that a client accepting a coding receives the content in it, under an entity tag
        /// of its own and with Vary, so no cache hands it to a client that cannot decode it.
        /// </summary>
        [Theory]
        [InlineData("br", "br", "-br\"")]
        [InlineData("gzip", "gzip", "-gz\"")]
        [InlineData("gzip, deflate, br, zstd", "br", "-br\"")]
        public void AcceptedCodingIsServed(string acceptEncoding, string expected, string eTagSuffix)
        {
            // arrange
            var componentHub = CreateComponentHub();

            // act
            var plain = Send(componentHub, Uri, "");
            var compressed = Send(componentHub, Uri, $"Accept-Encoding: {acceptEncoding}\n");
            var data = (byte[])compressed.Content;

            // validation
            Assert.IsType<ResponseOK>(compressed);
            Assert.Equal(expected, compressed.Header.CustomHeader["Content-Encoding"]);
            Assert.Equal("Accept-Encoding", compressed.Header.CustomHeader["Vary"]);
            Assert.Equal(data.Length, compressed.Header.ContentLength);
            Assert.Equal((byte[])plain.Content, Decompress(expected, data));
            Assert.EndsWith(eTagSuffix, compressed.Header.CustomHeader["ETag"]);
            Assert.NotEqual(plain.Header.CustomHeader["ETag"], compressed.Header.CustomHeader["ETag"]);
            Assert.Equal(plain.Header.ContentType, compressed.Header.ContentType);
        }

        /// <summary>
        /// Tests that a client that accepts no coding receives the plain content, which still
        /// varies by the header, since another client receives it compressed.
        /// </summary>
        [Fact]
        public void PlainContentWithoutAcceptedCoding()
        {
            // arrange
            var componentHub = CreateComponentHub();

            // act
            var response = Send(componentHub, Uri, "Accept-Encoding: identity\n");

            // validation
            Assert.Equal(UnitTestFixture.GetEmbeddedResource("css/mycss.css"), Encoding.UTF8.GetString((byte[])response.Content));
            Assert.False(response.Header.CustomHeader.ContainsKey("Content-Encoding"));
            Assert.Equal("Accept-Encoding", response.Header.CustomHeader["Vary"]);
        }

        /// <summary>
        /// Tests that compressing leaves the immutable cache policy of a versioned address alone.
        /// </summary>
        [Fact]
        public void VersionedAddressStaysImmutable()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var plugin = componentHub.PluginManager.GetPlugin(typeof(TestPlugin));
            var versioned = "http://localhost:8080" + componentHub.AssetManager.GetVersionedAssetRoute(application, plugin, "/assets/css/mycss.css");

            // act
            var response = Send(componentHub, versioned, "Accept-Encoding: br\n");

            // validation
            Assert.Equal("br", response.Header.CustomHeader["Content-Encoding"]);
            Assert.Equal("public, max-age=31536000, immutable", response.Header.CacheControl);
        }

        /// <summary>
        /// Tests that a client revalidating its compressed copy is told it is current, and that a
        /// client presenting the tag of another representation receives the content.
        /// </summary>
        [Fact]
        public void RevalidationComparesTheTagOfTheRepresentation()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var compressed = Send(componentHub, Uri, "Accept-Encoding: br\n");
            var plain = Send(componentHub, Uri, "");
            var compressedTag = compressed.Header.CustomHeader["ETag"];

            // act
            var current = Send(componentHub, Uri, $"Accept-Encoding: br\nIf-None-Match: {compressedTag}\n");
            var other = Send(componentHub, Uri, $"Accept-Encoding: br\nIf-None-Match: {plain.Header.CustomHeader["ETag"]}\n");

            // validation
            Assert.IsType<ResponseNotModified>(current);
            Assert.Equal(compressedTag, current.Header.CustomHeader["ETag"]);
            Assert.Equal("Accept-Encoding", current.Header.CustomHeader["Vary"]);
            Assert.IsType<ResponseOK>(other);
        }

        /// <summary>
        /// Tests that a range is cut from the plain content, since it counts the bytes of the
        /// file itself, even for a client that accepts a coding.
        /// </summary>
        [Fact]
        public void RangeIsServedUncompressed()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var plain = (byte[])Send(componentHub, Uri, "").Content;

            // act
            var part = Send(componentHub, Uri, "Accept-Encoding: br\nRange: bytes=2-9\n");

            // validation
            Assert.IsType<ResponsePartialContent>(part);
            Assert.Equal(plain[2..10], (byte[])part.Content);
            Assert.False(part.Header.CustomHeader.ContainsKey("Content-Encoding"));
        }

        /// <summary>
        /// Tests that content that is compressed already, or does not get smaller, is served as is.
        /// </summary>
        [Theory]
        [InlineData(ContentType.Png, "a repeated text a repeated text a repeated text a repeated text", false)]
        [InlineData(ContentType.Js, "x", false)]
        [InlineData(ContentType.Js, "a repeated text a repeated text a repeated text a repeated text", true)]
        public void OnlyWorthwhileContentIsCompressed(ContentType contentType, string content, bool expected)
        {
            // arrange
            var payload = new AssetPayload(Encoding.UTF8.GetBytes(content), contentType);

            // act
            var (_, _, encoding) = payload.Select("br, gzip");

            // validation
            Assert.Equal(expected, encoding is not null);
        }

        /// <summary>
        /// Tests that large content is not held back for brotli: a request is answered at once,
        /// with gzip until the brotli representation is done in the background, and with that
        /// afterwards.
        /// </summary>
        [Fact]
        public async Task LargeContentIsCompressedWithBrotliInTheBackground()
        {
            // arrange
            var content = string.Concat(Enumerable.Range(0, 4000).Select(x => $"var value{x} = {x};\n"));
            var payload = new AssetPayload(Encoding.UTF8.GetBytes(content), ContentType.Js);

            // act
            var (_, _, first) = payload.Select("br, gzip");
            var compressed = await payload.BrotliCompression;
            var (data, eTag, later) = payload.Select("br, gzip");

            // validation
            Assert.True(content.Length > 32 * 1024);
            Assert.Contains(first, new[] { "br", "gzip" });
            Assert.Equal("br", later);
            Assert.Same(compressed, data);
            Assert.EndsWith("-br\"", eTag);
        }

        /// <summary>
        /// Creates a component hub whose sitemap resolves the assets.
        /// </summary>
        /// <returns>The component hub.</returns>
        private static ComponentHub CreateComponentHub()
        {
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            componentHub.SitemapManager.Refresh();

            return componentHub;
        }

        /// <summary>
        /// Sends a GET request for a uri with the given header lines to the asset registered there.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="uri">The uri.</param>
        /// <param name="headers">The header lines, each ending in a line break.</param>
        /// <returns>The response.</returns>
        private static IResponse Send(IComponentHub componentHub, string uri, string headers)
        {
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var searchResult = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = UnitTestFixture.CreateHttpContextMock()
            });

            return componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock($"GET {uri} HTTP/1.1\n{headers}\n", uri), searchResult.EndpointContext);
        }

        /// <summary>
        /// Decodes content of the given coding.
        /// </summary>
        /// <param name="encoding">The content coding.</param>
        /// <param name="data">The encoded content.</param>
        /// <returns>The decoded content.</returns>
        private static byte[] Decompress(string encoding, byte[] data)
        {
            using var input = new MemoryStream(data);
            using Stream decoder = encoding == "br"
                ? new BrotliStream(input, CompressionMode.Decompress)
                : new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            decoder.CopyTo(output);

            return output.ToArray();
        }
    }
}
