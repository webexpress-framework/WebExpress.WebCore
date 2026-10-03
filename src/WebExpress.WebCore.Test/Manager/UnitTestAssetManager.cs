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
