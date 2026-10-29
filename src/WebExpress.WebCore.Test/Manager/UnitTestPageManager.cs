using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the page manager.
    /// </summary>
    public class UnitTestPageManager
    {
        /// <summary>
        /// Test the register function of the page manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(33, componentHub.PageManager.Pages.Count());
        }

        /// <summary>
        /// Test the remove function of the page manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var pageManager = componentHub.PageManager as PageManager;

            // act
            pageManager.Remove(plugin);

            Assert.Empty(componentHub.PageManager.Pages);
        }

        /// <summary>
        /// Test the id property of the page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Index), "webexpress.webcore.test.www.index")]
        [InlineData(typeof(TestApplicationA), typeof(About), "webexpress.webcore.test.www.about")]
        [InlineData(typeof(TestApplicationA), typeof(Contact), "webexpress.webcore.test.www.contact")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Index), "webexpress.webcore.test.www.blog.index")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.Index), "webexpress.webcore.test.www.blog.post.index")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.Add), "webexpress.webcore.test.www.blog.post.add")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.PostId.Edit), "webexpress.webcore.test.www.blog.post.postid.edit")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Blog.Post.PostId.Index), "webexpress.webcore.test.www.blog.post.postid.index")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Products.Index), "webexpress.webcore.test.www.products.index")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Products.List), "webexpress.webcore.test.www.products.list")]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Products.Details.Index), "webexpress.webcore.test.www.products.details.index")]
        public void Id(Type applicationType, Type pageType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var page = componentHub.PageManager.GetPages(pageType, application)?.FirstOrDefault();

            // act
            Assert.Equal(id, page.EndpointId.ToString());
        }

        /// <summary>
        /// Test the title property of the page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Index), "webindex:home.label")]
        [InlineData(typeof(TestApplicationA), typeof(About), "webindex:about.label")]
        [InlineData(typeof(TestApplicationA), typeof(Contact), "webindex:contact.label")]
        [InlineData(typeof(TestApplicationB), typeof(WWW.Index), "webindex:home.label")]
        [InlineData(typeof(TestApplicationB), typeof(About), "webindex:about.label")]
        [InlineData(typeof(TestApplicationB), typeof(Contact), "webindex:contact.label")]
        [InlineData(typeof(TestApplicationC), typeof(WWW.Index), "webindex:home.label")]
        [InlineData(typeof(TestApplicationC), typeof(About), "webindex:about.label")]
        [InlineData(typeof(TestApplicationC), typeof(Contact), "webindex:contact.label")]

        public void Title(Type applicationType, Type resourceType, string title)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var page = componentHub.PageManager.GetPages(resourceType, application)?.FirstOrDefault();

            // act
            Assert.Equal(title, page.PageTitle);
        }

        /// <summary>
        /// Test the context path property of the page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Index), "/server/appa")]
        [InlineData(typeof(TestApplicationA), typeof(About), "/server/appa/about")]
        [InlineData(typeof(TestApplicationA), typeof(Contact), "/server/appa/contact")]
        [InlineData(typeof(TestApplicationB), typeof(WWW.Index), "/server/appb")]
        [InlineData(typeof(TestApplicationB), typeof(About), "/server/appb/about")]
        [InlineData(typeof(TestApplicationB), typeof(Contact), "/server/appb/contact")]
        [InlineData(typeof(TestApplicationC), typeof(WWW.Index), "/server")]
        [InlineData(typeof(TestApplicationC), typeof(About), "/server/about")]
        [InlineData(typeof(TestApplicationC), typeof(Contact), "/server/contact")]
        public void RoutePath(Type applicationType, Type resourceType, string path)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var page = componentHub.PageManager.GetPages(resourceType, application)?.FirstOrDefault();

            // act
            Assert.Equal(path, page.Route.ToString());
        }

        /// <summary>
        /// Tests whether the page manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.PageManager.GetType()));
        }

        /// <summary>
        /// Tests whether the page context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var pages in componentHub.PageManager.Pages)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(pages.GetType()), $"Page context {pages.GetType().Name} does not implement IContext.");
            }
        }
    }
}
