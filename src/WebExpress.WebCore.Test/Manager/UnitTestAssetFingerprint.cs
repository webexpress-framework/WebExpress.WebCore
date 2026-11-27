using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSitemap;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that the files a stylesheet addresses are cached for good like scripts and
    /// stylesheets: they get an address carrying the hash of their content, a stylesheet is
    /// delivered pointing at that address, and a changed file changes the stylesheet's own address.
    /// </summary>
    public class UnitTestAssetFingerprint
    {
        private const string Stylesheet = "http://localhost:8080/server/appa/assets/css/myicon.css";

        /// <summary>
        /// Tests that an image is mounted on an address carrying its fingerprint, served immutable,
        /// while its plain address stays revalidated for whoever links it by that one.
        /// </summary>
        [Fact]
        public void ImageIsCachedLong()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var (application, plugin) = Resolve(componentHub);
            var versioned = componentHub.AssetManager.GetVersionedAssetRoute(application, plugin, "/assets/img/myicon.svg").ToString();

            // act
            var versionedResponse = Send(componentHub, "http://localhost:8080" + versioned);
            var plainResponse = Send(componentHub, "http://localhost:8080/server/appa/assets/img/myicon.svg");

            // validation
            Assert.Matches(@"^/server/appa/assets/img/myicon\.[0-9a-f]{12}\.svg$", versioned);
            Assert.Equal("public, max-age=31536000, immutable", versionedResponse.Header.CacheControl);
            Assert.Equal("no-cache", plainResponse.Header.CacheControl);
            Assert.Equal((byte[])plainResponse.Content, (byte[])versionedResponse.Content);
        }

        /// <summary>
        /// Tests that a stylesheet is delivered with each relative path of an embedded image
        /// rewritten to the fingerprinted name, keeping its quotes, query and fragment, and that
        /// every other url is left as written.
        /// </summary>
        [Fact]
        public void StylesheetAddressesTheFingerprintedImage()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var version = Asset.ComputeVersion(Encoding.UTF8.GetBytes(UnitTestFixture.GetEmbeddedResource("img/myicon.svg")));

            // act
            var css = Encoding.UTF8.GetString((byte[])Send(componentHub, Stylesheet).Content);

            // validation
            Assert.Contains($"url(\"../img/myicon.{version}.svg\")", css);
            Assert.Contains($"url(../img/myicon.{version}.svg?v=1#part)", css);
            Assert.Contains($"url('./../img/myicon.{version}.svg')", css);
            Assert.Contains("url('data:image/svg+xml;utf8,<svg xmlns=\"x\"/>')", css);
            Assert.Contains("url(/img/myicon.svg)", css);
            Assert.Contains("url(https://example.com/img/myicon.svg)", css);
            Assert.Contains("url(\"missing.svg\")", css);
            Assert.Contains("url(\"../../img/myicon.svg\")", css);
        }

        /// <summary>
        /// Tests that every rewritten path, resolved against the address of the stylesheet, is an
        /// address an asset is mounted on - from the plain and from the versioned stylesheet alike,
        /// since both sit in the same directory.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RewrittenAddressesAreMounted(bool versioned)
        {
            // arrange
            var componentHub = CreateComponentHub();
            var (application, plugin) = Resolve(componentHub);
            var mounted = componentHub.AssetManager.GetAssets(application).Select(x => x.Route.ToString()).ToHashSet();
            var route = versioned
                ? componentHub.AssetManager.GetVersionedAssetRoute(application, plugin, "/assets/css/myicon.css").ToString()
                : "/server/appa/assets/css/myicon.css";

            // act
            var css = Encoding.UTF8.GetString((byte[])Send(componentHub, "http://localhost:8080" + route).Content);
            var targets = System.Text.RegularExpressions.Regex.Matches(css, @"url\(([""']?)(\.[^""')?#]*myicon\.[0-9a-f]{12}\.svg)")
                .Select(x => new System.Uri(new System.Uri("http://localhost" + route), x.Groups[2].Value).AbsolutePath)
                .ToList();

            // validation
            Assert.Equal(3, targets.Count);
            Assert.All(targets, x => Assert.Contains(x, mounted));
        }

        /// <summary>
        /// Tests that the fingerprint of a stylesheet is taken from the content it is delivered
        /// with, so a changed image changes the address of every stylesheet that addresses it.
        /// </summary>
        [Fact]
        public void StylesheetFingerprintFollowsTheRewrittenContent()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var (application, plugin) = Resolve(componentHub);
            var route = componentHub.AssetManager.GetVersionedAssetRoute(application, plugin, "/assets/css/myicon.css").ToString();

            // act
            var content = (byte[])Send(componentHub, "http://localhost:8080" + route).Content;
            var raw = Encoding.UTF8.GetBytes(UnitTestFixture.GetEmbeddedResource("css/myicon.css"));

            // validation
            Assert.EndsWith($".{Asset.ComputeVersion(content)}.css", route);
            Assert.NotEqual(Asset.ComputeVersion(raw), Asset.ComputeVersion(content));
        }

        /// <summary>
        /// Tests that a stylesheet without a path to rewrite is delivered byte for byte.
        /// </summary>
        [Fact]
        public void StylesheetWithoutImagesIsUnchanged()
        {
            // arrange
            var componentHub = CreateComponentHub();

            // act
            var css = Encoding.UTF8.GetString((byte[])Send(componentHub, "http://localhost:8080/server/appa/assets/css/mycss.css").Content);

            // validation
            Assert.Equal(UnitTestFixture.GetEmbeddedResource("css/mycss.css"), css);
        }

        /// <summary>
        /// Tests that a file neither a page nor a stylesheet addresses keeps only its plain address.
        /// </summary>
        [Theory]
        [InlineData("WebExpress.WebCore.Test.Assets.doc.manual.pdf")]
        [InlineData("WebExpress.WebCore.Test.Assets.data.export.zip")]
        public void OtherFilesHaveNoFingerprint(string resource)
        {
            // act
            var version = AssetFingerprint.GetVersion(typeof(UnitTestAssetFingerprint).Assembly, resource);

            // validation
            Assert.Null(version);
        }

        /// <summary>
        /// Tests that a link composed from the plain route of a file - a theme stylesheet, an
        /// application icon - is resolved to the route carrying its fingerprint, and that a route
        /// naming no such file is not.
        /// </summary>
        [Fact]
        public void PlainRouteResolvesToTheVersionedOne()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var (application, plugin) = Resolve(componentHub);
            var versioned = componentHub.AssetManager.GetVersionedAssetRoute(application, plugin, "/assets/css/myicon.css").ToString();

            // act
            var resolved = componentHub.AssetManager.GetVersionedAssetRoute("/server/appa/assets/css/myicon.css");

            // validation
            Assert.Equal(versioned, resolved?.ToString());
            Assert.Null(componentHub.AssetManager.GetVersionedAssetRoute(versioned));
            Assert.Null(componentHub.AssetManager.GetVersionedAssetRoute("/server/appa/api/style.css"));
            Assert.Null(componentHub.AssetManager.GetVersionedAssetRoute(null));
        }

        /// <summary>
        /// Tests that the icon of an application, shown on every page, is addressed by its
        /// fingerprint while it is an embedded file, and by its plain route otherwise.
        /// </summary>
        [Fact]
        public void ApplicationIconIsAddressedByItsFingerprint()
        {
            // arrange
            var componentHub = CreateComponentHub();
            var (application, _) = Resolve(componentHub);
            var updates = 0;
            componentHub.ApplicationManager.UpdateApplication += (_, _) => updates++;

            // act
            componentHub.ApplicationManager.SetApplicationIcon(application, "/assets/img/myicon.svg");
            var embedded = application.Icon.ToString();
            componentHub.ApplicationManager.SetApplicationIcon(application, "/assets/img/myicon.svg");
            componentHub.ApplicationManager.SetApplicationIcon(application, null);
            var declared = application.Icon.ToString();

            // validation
            Assert.Matches(@"^/server/appa/assets/img/myicon\.[0-9a-f]{12}\.svg$", embedded);
            Assert.Equal("/server/appa/assets/img/Logo.png", declared);
            Assert.Equal(2, updates);
        }

        /// <summary>
        /// Tests the resolution of a relative path against the directory of the stylesheet.
        /// </summary>
        [Theory]
        [InlineData("css/", "../icons/a.svg", "icons/a.svg")]
        [InlineData("css/", "./../icons/a.svg", "icons/a.svg")]
        [InlineData("css/", "img/a.png", "css/img/a.png")]
        [InlineData("css/sub/", "../../fonts/a.woff2", "fonts/a.woff2")]
        [InlineData("", "a.svg", "a.svg")]
        [InlineData("css/", "../../a.svg", null)]
        public void ResolvePath(string directory, string path, string expected)
        {
            // act
            var resolved = AssetFingerprint.Resolve(directory, path);

            // validation
            Assert.Equal(expected, resolved);
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
        /// Returns the first test application and the test plugin.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <returns>The application and the plugin.</returns>
        private static (WebApplication.IApplicationContext Application, WebPlugin.IPluginContext Plugin) Resolve(IComponentHub componentHub)
        {
            return
            (
                componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First(),
                componentHub.PluginManager.GetPlugin(typeof(TestPlugin))
            );
        }

        /// <summary>
        /// Sends a request for a uri through the sitemap to the asset registered there.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="uri">The uri.</param>
        /// <returns>The response.</returns>
        private static IResponse Send(IComponentHub componentHub, string uri)
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
                .HandleRequest(UnitTestFixture.CreateRequestMock("", uri), searchResult.EndpointContext);
        }
    }
}
