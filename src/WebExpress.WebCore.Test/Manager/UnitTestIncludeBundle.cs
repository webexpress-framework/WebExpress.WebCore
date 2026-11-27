using System.Text;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebInclude;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSitemap;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that the files of an include are joined into one bundle per type, which a page links
    /// instead of every single file, and that switching bundling off links the files again.
    /// </summary>
    public class UnitTestIncludeBundle
    {
        /// <summary>
        /// Tests that the mounted files of an include are linked as one bundle per type, on an
        /// address that carries the hash of the joined content.
        /// </summary>
        [Fact]
        public void MountedFilesAreLinkedAsOneBundlePerType()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();

            // act
            var include = componentHub.IncludeManager.GetIncludes(application, typeof(TestIncludeBundle)).First();
            var linked = include.LinkedFiles.ToList();

            // validation
            Assert.Equal(4, include.Files.Count());
            Assert.Collection
            (
                linked,
                x => Assert.Matches(@"^/server/appa/assets/js/webexpress\.webcore\.test\.testincludebundle\.bundle\.[0-9a-f]{12}\.js$", x.Route),
                x => Assert.Matches(@"^/server/appa/assets/css/webexpress\.webcore\.test\.testincludebundle\.bundle\.[0-9a-f]{12}\.css$", x.Route)
            );
            Assert.Equal([TypeInclude.JavaScript, TypeInclude.StyleSheet], linked.Select(x => x.Type));
        }

        /// <summary>
        /// Tests that a bundle holds its files in declared order, each without its byte order mark,
        /// and scripts separated so a trailing comment or a missing semicolon cannot join two of
        /// them into one statement.
        /// </summary>
        [Theory]
        [InlineData(".js", "js/myjavascript.js", "js/myjavascript.mini.js", "\n;\n")]
        [InlineData(".css", "css/mycss.css", "css/my-css.css", "\n")]
        public void BundleJoinsTheFilesInDeclaredOrder(string extension, string first, string second, string separator)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var include = componentHub.IncludeManager.GetIncludes(application, typeof(TestIncludeBundle)).First();
            var route = include.LinkedFiles.Single(x => x.Route.EndsWith(extension)).Route;
            componentHub.SitemapManager.Refresh();

            // act
            var response = Send(componentHub, "http://localhost:8080" + route);

            // validation
            Assert.IsType<ResponseOK>(response);
            Assert.Equal
            (
                UnitTestFixture.GetEmbeddedResource(first).TrimStart('﻿') + separator + UnitTestFixture.GetEmbeddedResource(second).TrimStart('﻿'),
                Encoding.UTF8.GetString((byte[])response.Content)
            );
            Assert.DoesNotContain((byte)0xEF, (byte[])response.Content);
            Assert.Equal("public, max-age=31536000, immutable", response.Header.CacheControl);
        }

        /// <summary>
        /// Tests that the bundle is addressed by the hash of its content, the same way a single
        /// file is, so a changed file is a new address and never a stale copy.
        /// </summary>
        [Fact]
        public void BundleAddressCarriesTheHashOfItsContent()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var include = componentHub.IncludeManager.GetIncludes(application, typeof(TestIncludeBundle)).First();
            var route = include.LinkedFiles.First().Route;
            componentHub.SitemapManager.Refresh();

            // act
            var content = (byte[])Send(componentHub, "http://localhost:8080" + route).Content;

            // validation
            Assert.Contains($".{Asset.ComputeVersion(content)}.js", route);
        }

        /// <summary>
        /// Tests that a file that is not mounted is not joined into a bundle but keeps its place
        /// without a route, so a page still resolves it when it renders.
        /// </summary>
        [Fact]
        public void UnmountedFilesStaySingle()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();

            // act
            var include = componentHub.IncludeManager.GetIncludes(application, typeof(TestIncludeJavaScriptA)).First();

            // validation
            Assert.Equal(include.Files, include.LinkedFiles);
        }

        /// <summary>
        /// Tests that every application the plugin is bound to links a bundle mounted in that
        /// application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/server/appa/assets/js/")]
        [InlineData(typeof(TestApplicationB), "/server/appb/assets/js/")]
        public void EveryApplicationMountsItsOwnBundle(Type applicationType, string prefix)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).First();
            var mounted = componentHub.AssetManager.GetAssets(application).Select(x => x.Route.ToString()).ToHashSet();

            // act
            var include = componentHub.IncludeManager.GetIncludes(application, typeof(TestIncludeBundle)).First();
            var route = include.LinkedFiles.First().Route;

            // validation
            Assert.StartsWith(prefix, route);
            Assert.Contains(route, mounted);
        }

        /// <summary>
        /// Tests that the bundle leaves together with its plugin.
        /// </summary>
        [Fact]
        public void BundleIsRemovedWithItsPlugin()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var plugin = componentHub.PluginManager.GetPlugin(typeof(TestPlugin));
            var route = componentHub.IncludeManager.GetIncludes(application, typeof(TestIncludeBundle)).First().LinkedFiles.First().Route;

            // act
            (componentHub.AssetManager as AssetManager).Remove(plugin);

            // validation
            Assert.DoesNotContain(componentHub.AssetManager.Assets, x => x.Route.ToString() == route);
        }

        /// <summary>
        /// Tests that with bundling switched off a page links every file on its own again.
        /// </summary>
        [Fact]
        public void SwitchedOffLinksEveryFile()
        {
            // arrange
            var componentHub = CreateComponentHub(bundle: false);
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();

            // act
            var include = componentHub.IncludeManager.GetIncludes(application, typeof(TestIncludeBundle)).First();

            // validation
            Assert.Equal(include.Files, include.LinkedFiles);
            Assert.All(include.LinkedFiles, x => Assert.Matches(@"\.[0-9a-f]{12}\.(js|css)$", x.Route));
            Assert.DoesNotContain(componentHub.AssetManager.Assets, x => x.Route.ToString().Contains(".bundle"));
        }

        /// <summary>
        /// Tests that a file that resolves to nothing is reported whether or not the files are
        /// bundled: a bundle leaves such a file out, and nothing but the log names it.
        /// </summary>
        [Fact]
        public void UnresolvedFilesAreReportedInBothModes()
        {
            // arrange
            var bundled = UnitTestFixture.CreateHttpServerContextMock(configuration: CreateConfiguration(true));
            var single = UnitTestFixture.CreateHttpServerContextMock(configuration: CreateConfiguration(false));

            // the hub logs far more than the include manager; none of it may push a warning out
            ((Log)bundled.Log).RecentCapacity = int.MaxValue;
            ((Log)single.Log).RecentCapacity = int.MaxValue;

            // act
            UnitTestFixture.CreateAndRegisterComponentHubMock(bundled);
            UnitTestFixture.CreateAndRegisterComponentHubMock(single);

            // validation
            var bundledWarnings = UnresolvedWarnings(bundled);
            Assert.NotEmpty(bundledWarnings);
            Assert.Equal(UnresolvedWarnings(single), bundledWarnings);
        }

        /// <summary>
        /// Returns the warnings about include files that resolve to nothing, in a stable order.
        /// </summary>
        /// <param name="httpServerContext">The server context whose log is read.</param>
        /// <returns>The messages of the warnings.</returns>
        private static List<string> UnresolvedWarnings(IHttpServerContext httpServerContext)
        {
            return [.. httpServerContext.Log.GetRecentEntries()
                .Where(x => x.Level == LogLevel.Warning && x.Instance == "IncludeManager.WarnAboutUnresolvedFiles")
                .Select(x => x.Message)
                .Order(StringComparer.Ordinal)];
        }

        /// <summary>
        /// Tests the joining of contents: the byte order mark is removed from every part, and a
        /// script ending in a comment does not swallow the next one.
        /// </summary>
        [Fact]
        public void JoinSeparatesScriptsAndDropsByteOrderMarks()
        {
            // arrange
            byte[] bom = [0xEF, 0xBB, 0xBF];
            byte[] first = [.. bom, .. "let a = 1 // end"u8];
            byte[] second = [.. bom, .. "let b = 2"u8];

            // act
            var script = Encoding.UTF8.GetString(AssetBundle.Join(ContentType.Js, [first, second]));
            var style = Encoding.UTF8.GetString(AssetBundle.Join(ContentType.Css, [first, second]));

            // validation
            Assert.Equal("let a = 1 // end\n;\nlet b = 2", script);
            Assert.Equal("let a = 1 // end\nlet b = 2", style);
        }

        /// <summary>
        /// Creates a component hub with bundling switched on or off.
        /// </summary>
        /// <param name="bundle">Whether the files of an include are bundled.</param>
        /// <returns>The registered component hub.</returns>
        private static ComponentHub CreateComponentHub(bool bundle)
        {
            return UnitTestFixture.CreateAndRegisterComponentHubMock
            (
                UnitTestFixture.CreateHttpServerContextMock(configuration: CreateConfiguration(bundle))
            );
        }

        /// <summary>
        /// Creates a configuration that switches bundling on or off.
        /// </summary>
        /// <param name="bundle">Whether the files of an include are bundled.</param>
        /// <returns>The configuration.</returns>
        private static IConfigurationRoot CreateConfiguration(bool bundle)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["WebExpress:Assets:Bundle"] = bundle ? "true" : "false"
            }).Build();
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
