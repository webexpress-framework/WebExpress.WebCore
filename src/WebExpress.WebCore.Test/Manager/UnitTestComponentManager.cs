using WebExpress.WebCore.Test.Fixture;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the component manager.
    /// </summary>
    public class UnitTestComponentManager
    {
        /// <summary>
        /// Test the plugin manager property of the component manager.
        /// </summary>
        [Fact]
        public void PluginManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();

            // act
            Assert.NotNull(componentHub.PluginManager);
        }

        /// <summary>
        /// Test the application manager property of the component manager.
        /// </summary>
        [Fact]
        public void ApplicationManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();

            // act
            Assert.NotNull(componentHub.ApplicationManager);
        }
    }
}
