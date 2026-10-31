using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebSocket;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the websocket manager.
    /// </summary>
    public class UnitTestSocketManager
    {
        /// <summary>
        /// Test the register function of the socket manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            var socketManager = componentHub.SocketManager as SocketManager;

            // act
            pluginManager.Register();

            // validation
            Assert.Equal(3, socketManager.Sockets.Count());
            Assert.Equal("webexpress.webcore.test.testsocketa", socketManager.GetSockets<TestSocketA>()?.FirstOrDefault()?.EndpointId?.ToString());
            Assert.Equal("webexpress.webcore.test.testsocketa", socketManager.GetSockets(typeof(TestSocketA))?.FirstOrDefault()?.EndpointId?.ToString());
        }

        /// <summary>
        /// Test the remove function of the socket manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var socketManager = componentHub.SocketManager as SocketManager;
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            socketManager.Remove(plugin);

            // validation
            Assert.Empty(socketManager.Sockets);
        }

        /// <summary>
        /// Test the id property of the socket.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "webexpress.webcore.test.testsocketa")]
        [InlineData(typeof(TestApplicationB), "webexpress.webcore.test.testsocketa")]
        [InlineData(typeof(TestApplicationC), "webexpress.webcore.test.testsocketa")]
        public void Id(Type applicationType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationContext = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var socket = componentHub.SocketManager.GetSockets<TestSocketA>(applicationContext)
                .FirstOrDefault();

            // act
            Assert.Equal(id, socket.EndpointId?.ToString());
        }

        /// <summary>
        /// Test the context path property of the socket.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/server/appa/testsocketa")]
        [InlineData(typeof(TestApplicationB), "/server/appb/testsocketa")]
        [InlineData(typeof(TestApplicationC), "/server/testsocketa")]
        public void ContextPath(Type applicationType, string contextPath)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationContext = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var socket = componentHub.SocketManager.GetSockets<TestSocketA>(applicationContext)
                .FirstOrDefault();

            // act
            Assert.Equal(contextPath, socket.Route.ToString());
        }

        /// <summary>
        /// Verifies that a plain http request to a socket endpoint, which never passed the
        /// handshake, is told to upgrade instead of failing the dispatch with a server error.
        /// </summary>
        [Fact]
        public void HandleRequest_WithoutHandshake_RequiresUpgrade()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var socket = componentHub.SocketManager.Sockets.First();
            var request = UnitTestFixture.CreateRequestMock();

            // act
            var response = componentHub.EndpointManager.HandleRequest(request, socket);

            // validation
            var upgrade = Assert.IsType<ResponseUpgradeRequired>(response);
            Assert.Equal(426, upgrade.Status);
            Assert.Equal("websocket", upgrade.Header.Upgrade);
        }

        /// <summary>
        /// Tests whether the socket manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.SocketManager.GetType()));
        }

        /// <summary>
        /// Tests whether the application context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var application in componentHub.SocketManager.Sockets)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(application.GetType()), $"Socket context {application.GetType().Name} does not implement IContext.");
            }
        }
    }
}
