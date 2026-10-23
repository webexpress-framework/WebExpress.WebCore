using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW.Api._1_;
using WebExpress.WebCore.Test.WWW.Api._2;
using WebExpress.WebCore.Test.WWW.Api.V3;
using WebExpress.WebCore.Test.WWW.Resources;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebSitemap;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the sitemap manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSitemapManager
    {
        /// <summary>
        /// Test the refresh function of the sitemap manager.
        /// </summary>
        [Theory]
        [InlineData(106)]
        public void Refresh(int expected)
        {
            // arrange
            var componentManager = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            componentManager.SitemapManager.Refresh();

            // validation
            Assert.Equal(expected, componentManager.SitemapManager.SiteMap.Count());
        }

        /// <summary>
        /// Test the search resource function of the sitemap.
        /// </summary>
        [Theory]
        [InlineData("http://localhost:8080/server/appa/resources/testresourcea", "webexpress.webcore.test.www.resources.testresourcea")]
        [InlineData("http://localhost:8080/server/appa/resources/testresourceb", "webexpress.webcore.test.www.resources.testresourceb")]
        [InlineData("http://localhost:8080/server/appa/resources/testresourcec", "webexpress.webcore.test.www.resources.testresourcec")]
        [InlineData("http://localhost:8080/server/appa/resources/testresourced", "webexpress.webcore.test.www.resources.testresourced")]
        [InlineData("http://localhost:8080/server/appb/resources/testresourcea", "webexpress.webcore.test.www.resources.testresourcea")]
        [InlineData("http://localhost:8080/server/appb/resources/testresourceb", "webexpress.webcore.test.www.resources.testresourceb")]
        [InlineData("http://localhost:8080/server/appb/resources/testresourcec", "webexpress.webcore.test.www.resources.testresourcec")]
        [InlineData("http://localhost:8080/server/appb/resources/testresourced", "webexpress.webcore.test.www.resources.testresourced")]
        [InlineData("http://localhost:8080/server/resources/testresourcea", "webexpress.webcore.test.www.resources.testresourcea")]
        [InlineData("http://localhost:8080/server/resources/testresourceb", "webexpress.webcore.test.www.resources.testresourceb")]
        [InlineData("http://localhost:8080/server/resources/testresourcec", "webexpress.webcore.test.www.resources.testresourcec")]
        [InlineData("http://localhost:8080/server/resources/testresourced", "webexpress.webcore.test.www.resources.testresourced")]
        [InlineData("http://localhost:8080/server/appa", "webexpress.webcore.test.www.index")]
        [InlineData("http://localhost:8080/server/appa/about", "webexpress.webcore.test.www.about")]
        [InlineData("http://localhost:8080/server/appa/contact", "webexpress.webcore.test.www.contact")]
        [InlineData("http://localhost:8080/server/appb", "webexpress.webcore.test.www.index")]
        [InlineData("http://localhost:8080/server/appb/about", "webexpress.webcore.test.www.about")]
        [InlineData("http://localhost:8080/server/appb/contact", "webexpress.webcore.test.www.contact")]
        [InlineData("http://localhost:8080/server", "webexpress.webcore.test.www.index")]
        [InlineData("http://localhost:8080/server/about", "webexpress.webcore.test.www.about")]
        [InlineData("http://localhost:8080/server/contact", "webexpress.webcore.test.www.contact")]
        [InlineData("http://localhost:8080/server/appa/api/1/testrestapia", "webexpress.webcore.test.www.api._1_.testrestapia")]
        [InlineData("http://localhost:8080/server/appa/api/2/testrestapib", "webexpress.webcore.test.www.api._2.testrestapib")]
        [InlineData("http://localhost:8080/server/appa/api/3/testrestapic", "webexpress.webcore.test.www.api.v3.testrestapic")]
        [InlineData("http://localhost:8080/server/appa/assets/css/mycss.css", "webexpress.webcore.test.css.mycss.css")]
        [InlineData("http://localhost:8080/server/appa/assets/js/myjavascript.js", "webexpress.webcore.test.js.myjavascript.js")]
        [InlineData("http://localhost:8080/server/appa/assets/js/myjavascript.mini.js", "webexpress.webcore.test.js.myjavascript.mini.js")]
        [InlineData("http://localhost:8080/uri/does/not/exist", null)]
        public void SearchResource(string uri, string id)
        {
            // arrange
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

            componentHub.EndpointManager.HandleRequest(UnitTestFixture.CreateRequestMock(), searchResult?.EndpointContext);

            // validation
            Assert.Equal(id, searchResult?.EndpointContext?.EndpointId.ToString());
        }

        /// <summary>
        /// Test the get uri function of the sitemap.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceA), null, "/server/appa/resources/testresourcea")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceB), null, "/server/appa/resources/testresourceb")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceC), null, "/server/appa/resources/testresourcec")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceD), null, "/server/appa/resources/testresourced")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiA), null, "/server/appa/api/1/testrestapia")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiB), null, "/server/appa/api/2/testrestapib")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiC), null, "/server/appa/api/3/testrestapic")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Index), null, "/server/appa")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.About), null, "/server/appa/about")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Contact), null, "/server/appa/contact")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Index), null, "/server/appa/blog")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.Index), null, "/server/appa/blog/post")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.Add), null, "/server/appa/blog/post/add")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.PostId.Index), null, "/server/appa/blog/post/${testparametera}")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.PostId.Edit), null, "/server/appa/blog/post/${testparametera}/edit")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.PostId.Index), 1, "/server/appa/blog/post/1")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.PostId.Edit), 1, "/server/appa/blog/post/1/edit")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Products.Index), null, "/server/appa/products")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Products.List), null, "/server/appa/products/list")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Products.Details.Index), null, "/server/appa/products/${testparametera}")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Products.Details.Index), 2, "/server/appa/products/2")]
        public void GetUri(Type applicationType, Type resourceType, int? param, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            componentHub.SitemapManager.Refresh();
            var parameter = param.HasValue
                ? new TestParameterA(param ?? 0)
                : new TestParameterA();

            // act
            var uri = componentHub.SitemapManager.GetUri
            (
                resourceType,
                application,
                [
                    parameter
                ]
            );

            // validation
            Assert.Equal(expected, uri?.ToString());
        }

        /// <summary>
        /// Uses the configured public URI instead of the listener binding for absolute sitemap links.
        /// </summary>
        [Fact]
        public void GetUriUsesExternalUri()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(externalUri: "https://www.example.com/");
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock(httpServerContext);
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault();
            componentHub.SitemapManager.Refresh();

            // act
            var uri = componentHub.SitemapManager.GetUri(typeof(TestResourceA), application);

            // validation
            Assert.Equal("https://www.example.com/server/appa/resources/testresourcea", uri?.ToString());
        }

        /// <summary>
        /// Test the get endpoint function of the sitemap.
        /// </summary>
        [Theory]
        [InlineData("http://localhost:8080/uri/does/not/exist", null)]
        [InlineData("http://localhost:8080/server/appa/resources/testresourcea", "webexpress.webcore.test.www.resources.testresourcea")]
        [InlineData("http://localhost:8080/server/appa/resources/testresourceb", "webexpress.webcore.test.www.resources.testresourceb")]
        [InlineData("http://localhost:8080/server/appa/resources/testresourcec", "webexpress.webcore.test.www.resources.testresourcec")]
        [InlineData("http://localhost:8080/server/appa/resources/testresourced", "webexpress.webcore.test.www.resources.testresourced")]
        [InlineData("http://localhost:8080/server/appa/assets/css/mycss.css", "webexpress.webcore.test.css.mycss.css")]
        [InlineData("http://localhost:8080/server/appa/assets/js/myjavascript.js", "webexpress.webcore.test.js.myjavascript.js")]
        [InlineData("http://localhost:8080/server/appa/assets/js/myjavascript.mini.js", "webexpress.webcore.test.js.myjavascript.mini.js")]
        [InlineData("http://localhost:8080/server/appa/api/1/testrestapia", "webexpress.webcore.test.www.api._1_.testrestapia")]
        [InlineData("http://localhost:8080/server/appa/api/2/TestRestApiB", "webexpress.webcore.test.www.api._2.testrestapib")]
        [InlineData("http://localhost:8080/server/appa/api/3/testrestapic", "webexpress.webcore.test.www.api.v3.testrestapic")]
        [InlineData("http://localhost:8080/server/appa", "webexpress.webcore.test.www.index")]
        [InlineData("http://localhost:8080/server/appa/", "webexpress.webcore.test.www.index")]
        [InlineData("http://localhost:8080/server/appa/about", "webexpress.webcore.test.www.about")]
        [InlineData("http://localhost:8080/server/appa/About", "webexpress.webcore.test.www.about")]
        [InlineData("http://localhost:8080/server/appa/about/", "webexpress.webcore.test.www.about")]
        [InlineData("http://localhost:8080/server/appa/About/", "webexpress.webcore.test.www.about")]
        [InlineData("http://localhost:8080/server/appa/contact", "webexpress.webcore.test.www.contact")]
        [InlineData("http://localhost:8080/server/appa/blog", "webexpress.webcore.test.www.blog.index")]
        [InlineData("http://localhost:8080/server/appa/blog/post", "webexpress.webcore.test.www.blog.post.index")]
        [InlineData("http://localhost:8080/server/appa/blog/post/add", "webexpress.webcore.test.www.blog.post.add")]
        [InlineData("http://localhost:8080/server/appa/blog/post/10E96737-5C72-4C25-9E74-F96D8863D123/edit", "webexpress.webcore.test.www.blog.post.postid.edit")]
        [InlineData("http://localhost:8080/server/appa/blog/post/10E96737-5C72-4C25-9E74-F96D8863D123", "webexpress.webcore.test.www.blog.post.postid.index")]
        [InlineData("http://localhost:8080/server/appa/blog/post/10E96737-5C72-4C25-9E74-F96D8863D123/", "webexpress.webcore.test.www.blog.post.postid.index")]
        [InlineData("http://localhost:8080/server/appa/Products", "webexpress.webcore.test.www.products.index")]
        [InlineData("http://localhost:8080/server/appa/Products/", "webexpress.webcore.test.www.products.index")]
        [InlineData("http://localhost:8080/server/appa/Products/list", "webexpress.webcore.test.www.products.list")]
        [InlineData("http://localhost:8080/server/appa/products/10E96737-5C72-4C25-9E74-F96D8863D123", "webexpress.webcore.test.www.products.details.index")]
        [InlineData("http://localhost:8080/server/appa/products/10E96737-5C72-4C25-9E74-F96D8863D123/", "webexpress.webcore.test.www.products.details.index")]
        public void GetEndpoint(string uri, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            componentHub.SitemapManager.Refresh();

            // act
            var endpoint = componentHub.SitemapManager.GetEndpoint(new UriEndpoint(uri));

            // validation
            AssertExtensions.EqualWithPlaceholders(expected, endpoint?.EndpointId?.ToString());
        }

        /// <summary>
        /// Tests whether the sitemap manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.SitemapManager.GetType()));
        }
    }
}
