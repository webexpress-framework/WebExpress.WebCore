using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSitemap;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the asset manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestAssetManager
    {
        /// <summary>
        /// Test the register function of the asset manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(12, componentHub.AssetManager.Assets.Count());
        }

        /// <summary>
        /// Test the remove function of the asset manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var resourceManager = componentHub.AssetManager as AssetManager;

            // act
            resourceManager.Remove(plugin);

            Assert.Empty(componentHub.AssetManager.Assets);
        }

        /// <summary>
        /// Test the id property of the asset.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "webexpress.webcore.test.css.mycss.css")]
        [InlineData(typeof(TestApplicationA), "webexpress.webcore.test.css.my-css.css")]
        [InlineData(typeof(TestApplicationA), "webexpress.webcore.test.js.myjavascript.js")]
        [InlineData(typeof(TestApplicationA), "webexpress.webcore.test.js.myjavascript.mini.js")]
        [InlineData(typeof(TestApplicationB), "webexpress.webcore.test.css.mycss.css")]
        [InlineData(typeof(TestApplicationB), "webexpress.webcore.test.js.myjavascript.js")]
        [InlineData(typeof(TestApplicationB), "webexpress.webcore.test.js.myjavascript.mini.js")]
        [InlineData(typeof(TestApplicationC), "webexpress.webcore.test.css.mycss.css")]
        [InlineData(typeof(TestApplicationC), "webexpress.webcore.test.js.myjavascript.js")]
        [InlineData(typeof(TestApplicationC), "webexpress.webcore.test.js.myjavascript.mini.js")]
        public void Id(Type applicationType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var asset = componentHub.AssetManager.GetAssets(application)?.FirstOrDefault(x => x.EndpointId.ToString() == id);

            // act
            Assert.Equal(id, asset?.EndpointId.ToString());
        }

        /// <summary>
        /// Test the uri property of the asset.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/server/appa/assets/css/mycss.css")]
        [InlineData(typeof(TestApplicationA), "/server/appa/assets/css/my-css.css")]
        [InlineData(typeof(TestApplicationA), "/server/appa/assets/js/myjavascript.js")]
        [InlineData(typeof(TestApplicationA), "/server/appa/assets/js/myjavascript.mini.js")]
        [InlineData(typeof(TestApplicationB), "/server/appb/assets/css/mycss.css")]
        [InlineData(typeof(TestApplicationB), "/server/appb/assets/js/myjavascript.js")]
        [InlineData(typeof(TestApplicationB), "/server/appb/assets/js/myjavascript.mini.js")]
        [InlineData(typeof(TestApplicationC), "/server/assets/css/mycss.css")]
        [InlineData(typeof(TestApplicationC), "/server/assets/js/myjavascript.js")]
        [InlineData(typeof(TestApplicationC), "/server/assets/js/myjavascript.mini.js")]
        public void Uri(Type applicationType, string route)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var asset = componentHub.AssetManager.GetAssets(application)?
                .FirstOrDefault(x => x.Route.ToString() == route);

            // act
            Assert.Equal(route, asset?.Route.ToString());
        }

        /// <summary>
        /// Tests that the route resolved for a file of a plugin is the route the asset is
        /// really mounted on. Consumers that link an embedded file - the includes a page
        /// renders as link and script elements - resolve it this way instead of composing
        /// the route themselves, because a route that disagrees with the mount answers 404
        /// and a browser accepts that html error page as a stylesheet with no rules.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/assets/css/mycss.css", "/server/appa/assets/css/mycss.css")]
        [InlineData(typeof(TestApplicationA), "/assets/js/myjavascript.js", "/server/appa/assets/js/myjavascript.js")]
        [InlineData(typeof(TestApplicationB), "/assets/css/mycss.css", "/server/appb/assets/css/mycss.css")]
        [InlineData(typeof(TestApplicationC), "/assets/css/mycss.css", "/server/assets/css/mycss.css")]
        public void AssetRoute(Type applicationType, string file, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            var route = componentHub.AssetManager.GetAssetRoute(application, plugin, file);

            // validation
            Assert.Equal(expected, route?.ToString());
            Assert.Contains
            (
                expected,
                componentHub.AssetManager.GetAssets(application).Select(x => x.Route.ToString())
            );
        }

        /// <summary>
        /// Tests that a resolution which does not describe a file answers with nothing rather
        /// than with a route that leads nowhere.
        /// </summary>
        [Fact]
        public void AssetRouteWithoutFile()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA))?.FirstOrDefault();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act & validation
            Assert.Null(componentHub.AssetManager.GetAssetRoute(application, plugin, null));
            Assert.Null(componentHub.AssetManager.GetAssetRoute(application, null, "/assets/css/mycss.css"));
            Assert.Null(componentHub.AssetManager.GetAssetRoute(null, plugin, "/assets/css/mycss.css"));
        }

        /// <summary>
        /// Test the request of the asset.
        /// </summary>
        [Theory]
        [InlineData("http://localhost:8080/server/appa/assets/css/mycss.css", "css/mycss.css")]
        [InlineData("http://localhost:8080/server/appa/assets/css/my-css.css", "css/my-css.css")]
        [InlineData("http://localhost:8080/server/appa/assets/js/myjavascript.js", "js/myjavascript.js")]
        [InlineData("http://localhost:8080/server/appa/assets/js/myjavascript.mini.js", "js/myjavascript.mini.js")]
        [InlineData("http://localhost:8080/server/appb/assets/css/mycss.css", "css/mycss.css")]
        [InlineData("http://localhost:8080/server/appb/assets/js/myjavascript.js", "js/myjavascript.js")]
        [InlineData("http://localhost:8080/server/appb/assets/js/myjavascript.mini.js", "js/myjavascript.mini.js")]
        [InlineData("http://localhost:8080/server/assets/css/mycss.css", "css/mycss.css")]
        [InlineData("http://localhost:8080/server/assets/js/myjavascript.js", "js/myjavascript.js")]
        [InlineData("http://localhost:8080/server/assets/js/myjavascript.mini.js", "js/myjavascript.mini.js")]
        public void Request(string uri, string resource)
        {
            // arrange
            var embeddedResource = UnitTestFixture.GetEmbeddedResource(resource);
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var context = UnitTestFixture.CreateHttpContextMock();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            componentHub.SitemapManager.Refresh();

            // act
            var searchResult = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = context
            });

            var response = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock("", uri), searchResult.EndpointContext);

            Assert.Equal($"webexpress.webcore.test.{resource.Replace('/', '.')}", searchResult?.EndpointContext?.EndpointId.ToString());
            Assert.IsNotType<ResponseNotFound>(response);
            Assert.Equal(embeddedResource, Encoding.UTF8.GetString(response.Content as byte[]));
        }

        /// <summary>
        /// Tests that the asset response carries the content type derived from the file extension.
        /// </summary>
        [Theory]
        [InlineData("http://localhost:8080/server/appa/assets/css/mycss.css", "text/css")]
        [InlineData("http://localhost:8080/server/appa/assets/js/myjavascript.js", "text/javascript")]
        public void ContentType(string uri, string expectedContentType)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var context = UnitTestFixture.CreateHttpContextMock();
            componentHub.SitemapManager.Refresh();

            var searchResult = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = context
            });

            // act
            var response = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock("", uri), searchResult.EndpointContext);

            // validation
            Assert.Equal(expectedContentType, response.Header.ContentType);
        }

        /// <summary>
        /// Tests that the asset response exposes a non-empty ETag for cache validation.
        /// </summary>
        [Fact]
        public void ETagIsSet()
        {
            // arrange
            var uri = "http://localhost:8080/server/appa/assets/css/mycss.css";
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var context = UnitTestFixture.CreateHttpContextMock();
            componentHub.SitemapManager.Refresh();

            var searchResult = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = context
            });

            // act
            var response = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock("", uri), searchResult.EndpointContext);

            // validation
            Assert.True(response.Header.CustomHeader.ContainsKey("ETag"));
            Assert.False(string.IsNullOrEmpty(response.Header.CustomHeader["ETag"]));
        }

        /// <summary>
        /// Tests that a request whose If-None-Match matches the current ETag is answered with
        /// 304 Not Modified instead of resending the payload.
        /// </summary>
        [Fact]
        public void ConditionalRequestReturnsNotModified()
        {
            // arrange
            var uri = "http://localhost:8080/server/appa/assets/css/mycss.css";
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var context = UnitTestFixture.CreateHttpContextMock();
            componentHub.SitemapManager.Refresh();

            var searchResult = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = context
            });

            // act - first request returns the payload together with its ETag
            var first = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock("", uri), searchResult.EndpointContext);
            var eTag = first.Header.CustomHeader["ETag"];

            // act - second request presents the ETag via If-None-Match
            var conditional = $"GET {uri} HTTP/1.1\nIf-None-Match: {eTag}\n\n";
            var second = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock(conditional, uri), searchResult.EndpointContext);

            // validation
            Assert.IsType<ResponseOK>(first);
            Assert.IsType<ResponseNotModified>(second);
        }

        /// <summary>
        /// Tests that a range request is answered with the requested part, so a media player can
        /// seek, and that the full answer announces the support for ranges.
        /// </summary>
        [Fact]
        public void RangeRequestReturnsPartialContent()
        {
            // arrange
            var uri = "http://localhost:8080/server/appa/assets/css/mycss.css";
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var context = UnitTestFixture.CreateHttpContextMock();
            componentHub.SitemapManager.Refresh();

            var searchResult = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = context
            });

            // act
            var full = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock("", uri), searchResult.EndpointContext);
            var data = (byte[])full.Content;
            var eTag = full.Header.CustomHeader["ETag"];

            var part = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock($"GET {uri} HTTP/1.1\nRange: bytes=2-9\n\n", uri), searchResult.EndpointContext);
            var outdated = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock($"GET {uri} HTTP/1.1\nRange: bytes=2-9\nIf-Range: \"outdated\"\n\n", uri), searchResult.EndpointContext);
            var current = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock($"GET {uri} HTTP/1.1\nRange: bytes=2-9\nIf-Range: {eTag}\n\n", uri), searchResult.EndpointContext);
            var outside = componentHub
                .EndpointManager
                .HandleRequest(UnitTestFixture.CreateRequestMock($"GET {uri} HTTP/1.1\nRange: bytes={data.Length}-\n\n", uri), searchResult.EndpointContext);

            // validation
            Assert.IsType<ResponseOK>(full);
            Assert.Equal("bytes", full.Header.CustomHeader["Accept-Ranges"]);
            Assert.IsType<ResponsePartialContent>(part);
            Assert.Equal(data[2..10], (byte[])part.Content);
            Assert.Equal(8, part.Header.ContentLength);
            Assert.Equal($"bytes 2-9/{data.Length}", part.Header.CustomHeader["Content-Range"]);
            Assert.IsType<ResponseOK>(outdated);
            Assert.IsType<ResponsePartialContent>(current);
            Assert.IsType<ResponseRangeNotSatisfiable>(outside);
            Assert.Equal($"bytes */{data.Length}", outside.Header.CustomHeader["Content-Range"]);
        }

        /// <summary>
        /// Tests that methods other than GET ignore ranges and retain the full representation metadata.
        /// </summary>
        /// <param name="method">The request method for which ranges must be ignored.</param>
        /// <param name="range">The satisfiable or unsatisfiable range to ignore.</param>
        [Theory]
        [InlineData("HEAD", "bytes=2-9")]
        [InlineData("HEAD", "bytes=999999999-")]
        [InlineData("POST", "bytes=2-9")]
        [InlineData("POST", "bytes=999999999-")]
        public void NonGetRequestIgnoresRange(string method, string range)
        {
            // arrange
            var uri = "http://localhost:8080/server/appa/assets/css/mycss.css";
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var context = UnitTestFixture.CreateHttpContextMock();
            componentHub.SitemapManager.Refresh();
            var searchResult = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = context
            });

            // act
            var full = componentHub.EndpointManager.HandleRequest
            (
                UnitTestFixture.CreateRequestMock("", uri), searchResult.EndpointContext
            );
            var response = componentHub.EndpointManager.HandleRequest
            (
                UnitTestFixture.CreateRequestMock($"{method} {uri} HTTP/1.1\nRange: {range}\n\n", uri),
                searchResult.EndpointContext
            );

            // validation
            Assert.IsType<ResponseOK>(response);
            Assert.Equal(full.Header.ContentLength, response.Header.ContentLength);
            Assert.Equal(full.Header.ContentType, response.Header.ContentType);
            Assert.Equal(full.Header.CustomHeader["ETag"], response.Header.CustomHeader["ETag"]);
            Assert.False(response.Header.CustomHeader.ContainsKey("Content-Range"));
        }

        /// <summary>
        /// Tests the parsing of the Range header. A header that cannot be parsed or names several
        /// ranges yields the whole file (null), a range outside of the file (-1, -1).
        /// </summary>
        [Theory]
        [InlineData("bytes=0-99", 0L, 99L)]
        [InlineData("bytes=10-", 10L, 99L)]
        [InlineData("bytes=10-500", 10L, 99L)]
        [InlineData("bytes=-30", 70L, 99L)]
        [InlineData("bytes=-500", 0L, 99L)]
        [InlineData("bytes=100-", -1L, -1L)]
        [InlineData("bytes=-0", -1L, -1L)]
        [InlineData("bytes=20-10", null, null)]
        [InlineData("bytes=0-1,5-6", null, null)]
        [InlineData("items=0-1", null, null)]
        [InlineData("bytes=a-b", null, null)]
        [InlineData("bytes=5", null, null)]
        [InlineData("", null, null)]
        public void ParseRange(string header, long? start, long? end)
        {
            // act
            var range = Asset.ParseRange(header, 100);

            // validation
            Assert.Equal(start, range?.Start);
            Assert.Equal(end, range?.End);
        }

        /// <summary>
        /// Tests whether the asset manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.AssetManager.GetType()));
        }

        /// <summary>
        /// Tests whether the asset context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var asset in componentHub.AssetManager.Assets)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(asset.GetType()), $"Asset context {asset.GetType().Name} does not implement IContext.");
            }
        }
    }
}
