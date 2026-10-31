using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the status page manager.
    /// </summary>
    public class UnitTestStatusPageManager
    {
        /// <summary>
        /// Test the register function of the status page manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(12, componentHub.StatusPageManager.StatusPages.Count());
        }

        /// <summary>
        /// Test the remove function of the status page manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var statusPageManager = componentHub.StatusPageManager as StatusPageManager;

            // act
            statusPageManager.Remove(plugin);

            Assert.Empty(componentHub.StatusPageManager.StatusPages);
        }

        /// <summary>
        /// Test the id property of the status page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage301), "webexpress.webcore.test.teststatuspage301")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage400), "webexpress.webcore.test.teststatuspage400")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage404), "webexpress.webcore.test.teststatuspage404")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage500), "webexpress.webcore.test.teststatuspage500")]

        public void Id(Type applicationType, Type statusPageType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var statusPage = componentHub.StatusPageManager.GetStatusPage(application, statusPageType);

            // act
            Assert.Equal(id, statusPage.StatusPageId.ToString());
        }

        /// <summary>
        /// Test the title property of the status page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage301), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage400), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage404), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage500), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage301), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage400), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage404), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage500), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage301), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage400), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage404), "webindex:homepage.label")]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage500), "webindex:homepage.label")]
        public void Title(Type applicationType, Type resourceType, string title)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var statusPage = componentHub.StatusPageManager.GetStatusPage(application, resourceType);

            // act
            Assert.Equal(title, statusPage.StatusTitle);
        }

        /// <summary>
        /// Test the id property of the status page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage301), 301)]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage400), 400)]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage404), 404)]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage500), 500)]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage301), 301)]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage400), 400)]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage404), 404)]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage500), 500)]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage301), 301)]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage400), 400)]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage404), 404)]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage500), 500)]
        public void Code(Type applicationType, Type statusPageType, int? code)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var statusPage = componentHub.StatusPageManager.GetStatusPage(application, statusPageType);

            // act
            Assert.Equal(code, statusPage?.StatusCode);
        }

        /// <summary>
        /// Test the icon property of the status page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage301), null)]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage400), "/server/appa/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage404), "/server/appa/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationA), typeof(TestStatusPage500), "/server/appa/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage301), null)]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage400), "/server/appb/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage404), "/server/appb/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationB), typeof(TestStatusPage500), "/server/appb/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage301), null)]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage400), "/server/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage404), "/server/webexpress/icon.png")]
        [InlineData(typeof(TestApplicationC), typeof(TestStatusPage500), "/server/webexpress/icon.png")]
        public void Icon(Type applicationType, Type statusPageType, string icon)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var statusPage = componentHub.StatusPageManager.GetStatusPage(application, statusPageType);

            // act
            Assert.Equal(icon, statusPage?.StatusIcon?.ToString());
        }

        /// <summary>
        /// Test the CreateStatusResponse function of the status page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), 400, 400)]
        [InlineData(typeof(TestApplicationA), 404, 404)]
        [InlineData(typeof(TestApplicationB), 404, 404)]
        [InlineData(typeof(TestApplicationB), 500, 500)]
        [InlineData(typeof(TestApplicationA), 500, 500)]
        public void CreateAndCheckCode(Type applicationType, int statusCode, int? expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var statusResponse = componentHub.StatusPageManager.CreateStatusResponse("content", statusCode, application, UnitTestFixture.CreateHttpContextMock().Request);

            // act
            Assert.Equal(expected, statusResponse?.Status);
        }

        /// <summary>
        /// Test the CreateStatusResponse function of the status page.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), 400, "content", "content", 72)]
        [InlineData(typeof(TestApplicationA), 500, "content", "content", 72)]
        public void CreateAndCheckMessage(Type applicationType, int statusCode, string content, string expected, int length)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var statusResponse = componentHub.StatusPageManager.CreateStatusResponse(content, statusCode, application, UnitTestFixture.CreateHttpContextMock().Request);

            // validation
            var normalized = statusResponse?.Content?.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
            Assert.Contains(expected, statusResponse?.Content?.ToString());
            Assert.Equal(length, normalized.Length);
            Assert.Equal(statusResponse?.Content?.ToString().Length, statusResponse?.Header?.ContentLength);
        }

        /// <summary>
        /// Tests whether the status page manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.StatusPageManager.GetType()));
        }

        /// <summary>
        /// Tests whether the status page context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var application in componentHub.StatusPageManager.StatusPages)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(application.GetType()), $"Page context {application.GetType().Name} does not implement IContext.");
            }
        }
    }
}
