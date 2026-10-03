using System.Reflection;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebPlugin.Model;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the plugin manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestPluginManager
    {
        /// <summary>
        /// Test the register function of the plugin manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;

            // act
            pluginManager.Register();

            Assert.Single(componentHub.PluginManager?.Plugins);
            Assert.Contains("webexpress.webcore.test", componentHub.PluginManager?.GetPlugin(typeof(TestPlugin))?.PluginId.ToString());
        }

        /// <summary>
        /// Test the event of the plugin manager.
        /// </summary>
        [Fact]
        public void RegisterEvent()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            var i = 0;
            var triggered = false;

            componentHub.PluginManager?.AddPlugin += (s, e) => { i++; triggered = true; };

            // act
            pluginManager.Register();

            Assert.Single(componentHub.PluginManager?.Plugins);
            Assert.Contains("webexpress.webcore.test", componentHub.PluginManager?.GetPlugin(typeof(TestPlugin))?.PluginId.ToString());
            Assert.Equal(1, i);
            Assert.True(triggered);
        }

        /// <summary>
        /// Test the remove function of the plugin manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            pluginManager.Register();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            pluginManager.Remove(plugin);

            Assert.Empty(componentHub.PluginManager?.Plugins);
        }

        /// <summary>
        /// Test the event of the plugin manager.
        /// </summary>
        [Fact]
        public void RemoveEvent()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            var i = 1;
            var triggered = false;

            componentHub.PluginManager?.RemovePlugin += (s, e) => { i--; triggered = true; };
            pluginManager.Register();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            pluginManager.Remove(plugin);

            Assert.Empty(componentHub.PluginManager?.Plugins);
            Assert.Equal(0, i);
            Assert.True(triggered);
        }

        /// <summary>
        /// Removing a plugin releases it completely: the listeners of the removal event still
        /// find it intact, afterwards the plugin, its cancellation token source and the
        /// applications bound to it are disposed - all before the load context is unloaded.
        /// </summary>
        [Fact]
        public void RemoveDisposesPluginAndApplications()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            pluginManager.Register();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var dictionary = typeof(PluginManager)
                .GetField("_dictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(pluginManager) as PluginDictionary;
            var pluginItem = dictionary[plugin.PluginId];
            var pluginInstance = (TestPlugin)pluginItem.Plugin;
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).Single();
            bool? disposedDuringEvent = null;

            componentHub.PluginManager.RemovePlugin += (s, e) => disposedDuringEvent = pluginInstance.IsDisposed;

            // act
            pluginManager.Remove(plugin);

            // validation
            Assert.False(disposedDuringEvent);
            Assert.True(pluginInstance.IsDisposed);
            Assert.True(pluginItem.CancellationTokenSource.IsCancellationRequested);
            Assert.Throws<ObjectDisposedException>(() => pluginItem.CancellationTokenSource.Token);
            Assert.Empty(componentHub.ApplicationManager.Applications);
            Assert.Null(componentHub.ApplicationManager.GetApplication(application.ApplicationId));
        }

        /// <summary>
        /// A plugin's load context must be collectible, otherwise unloading it on removal fails.
        /// </summary>
        [Fact]
        public void PluginLoadContextIsCollectible()
        {
            // arrange
            var loadContext = new PluginLoadContext(typeof(TestPlugin).Assembly.Location);

            // act
            loadContext.Unload();

            // validation
            Assert.True(loadContext.IsCollectible);
        }

        /// <summary>
        /// Test the get plugin function of the plugin manager.
        /// </summary>
        [Fact]
        public void GetPluginById()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            var plugin = componentHub.PluginManager?.GetPlugin("webexpress.webcore.test");

            Assert.Equal("webexpress.webcore.test", plugin?.PluginId.ToString());
        }

        /// <summary>
        /// Test the get plugin function of the plugin manager.
        /// </summary>
        [Fact]
        public void GetPluginByType()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            Assert.Equal("webexpress.webcore.test", plugin?.PluginId.ToString());
        }

        /// <summary>
        /// Test the name property of the plugin.
        /// </summary>
        [Fact]
        public void Id()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            Assert.Equal(typeof(TestPlugin).Namespace.ToLower(), plugin.PluginId.ToString());
        }

        /// <summary>
        /// Test the name property of the plugin.
        /// </summary>
        [Fact]
        public void GetName()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            Assert.Equal("TestPlugin", plugin.PluginName);
        }

        /// <summary>
        /// Test the description property of the plugin.
        /// </summary>
        [Fact]
        public void GetDescription()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            Assert.Equal("plugin.description", plugin.Description);
        }

        /// <summary>
        /// Test the icon property of the plugin.
        /// </summary>
        [Fact]
        public void GetIcon()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            Assert.Equal("/server/assets/img/Logo.png", plugin.Icon.ToString());
        }

        /// <summary>
        /// Test the boot function of the plugin manager.
        /// </summary>
        [Theory]
        [InlineData("webexpress.webcore.test", "webexpress.webcore.test")]
        [InlineData("non.existent.plugin", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Boot(string pluginId, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            pluginManager.Register();
            var plugin = componentHub.PluginManager?.GetPlugin(pluginId);

            // act
            pluginManager.Boot(plugin);

            Assert.Single(componentHub.PluginManager?.Plugins);
            Assert.Equal(expected, plugin?.PluginId.ToString());
        }

        /// <summary>
        /// Test the shut down of the plugin manager.
        /// </summary>
        [Theory]
        [InlineData("webexpress.webcore.test", "webexpress.webcore.test")]
        [InlineData("non.existent.plugin", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ShutDown(string pluginId, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            pluginManager.Register();
            var plugin = componentHub.PluginManager?.GetPlugin(pluginId);

            // act
            pluginManager.ShutDown(plugin);

            Assert.Single(componentHub.PluginManager?.Plugins);
            Assert.Equal(expected, plugin?.PluginId.ToString());
        }

        /// <summary>
        /// Tests whether the plugin manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(pluginManager.GetType()));
        }

        /// <summary>
        /// Tests whether the plugin context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var plugin in componentHub.PluginManager?.Plugins)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(plugin.GetType()), $"Plugin context {plugin.GetType().Name} does not implement IContext.");
            }
        }

        /// <summary>
        /// Tests runtime plugin metadata retrieval.
        /// </summary>
        [Fact]
        public void GetPluginRuntimeInfos()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var pluginManager = componentHub.PluginManager;

            // act
            var infos = pluginManager.GetPluginRuntimeInfos().ToList();

            // validation
            Assert.NotEmpty(infos);
            Assert.Contains(infos, x => x.PluginContext.PluginId.ToString() == "webexpress.webcore.test");
            Assert.All(infos, x => Assert.Equal(PluginRuntimeState.Active, x.State));
        }
    }
}
