using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW.Resources;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebResource;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the resource manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestResourceManager
    {
        /// <summary>
        /// Test the register function of the resource manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(12, componentHub.ResourceManager.Resources.Count());
        }

        /// <summary>
        /// Test the remove function of the resource manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var resourceManager = componentHub.ResourceManager as ResourceManager;

            // act
            resourceManager.Remove(plugin);

            Assert.Empty(componentHub.ResourceManager.Resources);
        }

        /// <summary>
        /// Test the id property of the resource.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceA), "webexpress.webcore.test.www.resources.testresourcea")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceB), "webexpress.webcore.test.www.resources.testresourceb")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceC), "webexpress.webcore.test.www.resources.testresourcec")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceD), "webexpress.webcore.test.www.resources.testresourced")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceA), "webexpress.webcore.test.www.resources.testresourcea")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceB), "webexpress.webcore.test.www.resources.testresourceb")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceC), "webexpress.webcore.test.www.resources.testresourcec")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceD), "webexpress.webcore.test.www.resources.testresourced")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceA), "webexpress.webcore.test.www.resources.testresourcea")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceB), "webexpress.webcore.test.www.resources.testresourceb")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceC), "webexpress.webcore.test.www.resources.testresourcec")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceD), "webexpress.webcore.test.www.resources.testresourced")]
        public void Id(Type applicationType, Type resourceType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var resource = componentHub.ResourceManager.GetResources(resourceType, application)?.FirstOrDefault();

            // act
            Assert.Equal(id, resource?.EndpointId.ToString());
        }

        /// <summary>
        /// Test the context path property of the resource.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceA), "/server/appa/resources/testresourcea")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceB), "/server/appa/resources/testresourceb")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceC), "/server/appa/resources/testresourcec")]
        [InlineData(typeof(TestApplicationA), typeof(TestResourceD), "/server/appa/resources/testresourced")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceA), "/server/appb/resources/testresourcea")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceB), "/server/appb/resources/testresourceb")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceC), "/server/appb/resources/testresourcec")]
        [InlineData(typeof(TestApplicationB), typeof(TestResourceD), "/server/appb/resources/testresourced")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceA), "/server/resources/testresourcea")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceB), "/server/resources/testresourceb")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceC), "/server/resources/testresourcec")]
        [InlineData(typeof(TestApplicationC), typeof(TestResourceD), "/server/resources/testresourced")]

        public void RoutePath(Type applicationType, Type resourceType, string path)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var resource = componentHub.ResourceManager.GetResources(resourceType, application)?.FirstOrDefault();

            // act
            Assert.Equal(path, resource.Route.ToString());
        }

        /// <summary>
        /// Tests whether the resource manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.ResourceManager.GetType()));
        }

        /// <summary>
        /// Tests whether the resource context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var resources in componentHub.ResourceManager.Resources)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(resources.GetType()), $"Resource context {resources.GetType().Name} does not implement IContext.");
            }
        }
    }
}
