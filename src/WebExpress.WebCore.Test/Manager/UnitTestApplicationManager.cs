using System.Reflection;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebApplication.Model;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the application manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestApplicationManager
    {
        /// <summary>
        /// Test the register function of the application manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;

            // act
            pluginManager.Register();

            // validation
            Assert.Equal(3, componentHub.ApplicationManager.Applications.Count());
            Assert.Equal("webexpress.webcore.test.testapplicationa", componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault()?.ApplicationId);
            Assert.Equal("webexpress.webcore.test.testapplicationb", componentHub.ApplicationManager.GetApplications(typeof(TestApplicationB)).FirstOrDefault()?.ApplicationId);
            Assert.Equal("webexpress.webcore.test.testapplicationc", componentHub.ApplicationManager.GetApplications(typeof(TestApplicationC)).FirstOrDefault()?.ApplicationId);
        }

        /// <summary>
        /// Test the remove function of the application manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationManager = componentHub.ApplicationManager as ApplicationManager;
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            applicationManager.Remove(plugin);

            // validation
            Assert.Empty(applicationManager.Applications);
        }

        /// <summary>
        /// Removing the applications of a plugin disposes them and their cancellation token
        /// sources at once, so a hot unload leaves no application running on unloaded code.
        /// </summary>
        [Fact]
        public void RemoveDisposesApplicationsImmediately()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationManager = componentHub.ApplicationManager as ApplicationManager;
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var applicationItems = GetApplicationItems(applicationManager, plugin);

            // act
            applicationManager.Remove(plugin);

            // validation
            Assert.NotEmpty(applicationItems);
            Assert.True(applicationItems.Select(x => x.Application).OfType<TestApplicationA>().Single().IsDisposed);
            Assert.All(applicationItems, x =>
            {
                Assert.True(x.CancellationTokenSource.IsCancellationRequested);
                Assert.Throws<ObjectDisposedException>(() => x.CancellationTokenSource.Token);
            });
        }

        /// <summary>
        /// The listeners of the removal event see the application before it is disposed, so
        /// they can still release what they bound to it.
        /// </summary>
        [Fact]
        public void RemoveRaisesEventBeforeDisposing()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationManager = componentHub.ApplicationManager as ApplicationManager;
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var application = GetApplicationItems(applicationManager, plugin)
                .Select(x => x.Application)
                .OfType<TestApplicationA>()
                .Single();
            bool? disposedDuringEvent = null;

            applicationManager.RemoveApplication += (s, e) =>
            {
                if (e.ApplicationId == "webexpress.webcore.test.testapplicationa")
                {
                    disposedDuringEvent = application.IsDisposed;
                }
            };

            // act
            applicationManager.Remove(plugin);

            // validation
            Assert.False(disposedDuringEvent);
            Assert.True(application.IsDisposed);
        }

        /// <summary>
        /// Returns the registry entries of the applications of a plugin.
        /// </summary>
        /// <param name="applicationManager">The application manager.</param>
        /// <param name="plugin">The plugin.</param>
        /// <returns>The application entries.</returns>
        private static List<ApplicationItem> GetApplicationItems(ApplicationManager applicationManager, IPluginContext plugin)
        {
            var dictionary = typeof(ApplicationManager)
                .GetField("_dictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(applicationManager) as ApplicationDictionary;

            return [.. dictionary.GetApplicationItems(plugin)];
        }

        /// <summary>
        /// Test the id property of the application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "webexpress.webcore.test.testapplicationa")]
        [InlineData(typeof(TestApplicationB), "webexpress.webcore.test.testapplicationb")]
        [InlineData(typeof(TestApplicationC), "webexpress.webcore.test.testapplicationc")]
        public void Id(Type applicationType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            Assert.Equal(id, application.ApplicationId);
        }

        /// <summary>
        /// Test the name property of the application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "TestApplicationA")]
        [InlineData(typeof(TestApplicationB), "TestApplicationB")]
        [InlineData(typeof(TestApplicationC), "TestApplicationC")]
        public void Name(Type applicationType, string name)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            Assert.Equal(name, application.ApplicationName);
        }

        /// <summary>
        /// Test the description property of the application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "application.description")]
        [InlineData(typeof(TestApplicationB), "application.description")]
        [InlineData(typeof(TestApplicationC), "application.description")]
        public void Description(Type applicationType, string description)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            Assert.Equal(description, application.Description);
        }

        /// <summary>
        /// Test the icon property of the application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/server/appa/assets/img/Logo.png")]
        [InlineData(typeof(TestApplicationB), "/server/appb/assets/img/Logo.png")]
        [InlineData(typeof(TestApplicationC), "/server/assets/img/Logo.png")]
        public void Icon(Type applicationType, string icon)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            Assert.Equal(icon, application.Icon.ToString());
        }

        /// <summary>
        /// Test that the name of a registered application can be replaced at runtime, and that a
        /// blank value puts the declared name back.
        /// </summary>
        [Fact]
        public void SetApplicationName()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationManager = componentHub.ApplicationManager;
            var application = applicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault();
            var declared = application.ApplicationName;
            var updated = new List<IApplicationContext>();

            applicationManager.UpdateApplication += (_, context) => updated.Add(context);

            // act
            applicationManager.SetApplicationName(application, "Renamed");

            // validation
            Assert.Equal("Renamed", application.ApplicationName);
            Assert.Equal("webexpress.webcore.test.testapplicationa", application.ApplicationId);
            Assert.Single(updated);

            // a blank value restores what the application declared
            applicationManager.SetApplicationName(application, " ");

            Assert.Equal(declared, application.ApplicationName);
            Assert.Equal(2, updated.Count);
        }

        /// <summary>
        /// Test that renaming an application to the name it already carries changes nothing and
        /// raises no event.
        /// </summary>
        [Fact]
        public void SetApplicationNameUnchanged()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationManager = componentHub.ApplicationManager;
            var application = applicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault();
            var updated = 0;

            applicationManager.UpdateApplication += (_, _) => updated++;

            // act
            applicationManager.SetApplicationName(application, application.ApplicationName);

            // validation
            Assert.Equal(0, updated);
        }

        /// <summary>
        /// Test that the icon of a registered application can be replaced at runtime. The value is
        /// a path relative to the application, which the manager combines into a route the same
        /// way it does at registration.
        /// </summary>
        [Fact]
        public void SetApplicationIcon()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationManager = componentHub.ApplicationManager;
            var application = applicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault();
            var updated = new List<IApplicationContext>();

            applicationManager.UpdateApplication += (_, context) => updated.Add(context);

            // act
            applicationManager.SetApplicationIcon(application, "/assets/img/Custom.svg");

            // validation
            Assert.Equal("/server/appa/assets/img/Custom.svg", application.Icon.ToString());
            Assert.Single(updated);

            // a blank value restores what the application declared
            applicationManager.SetApplicationIcon(application, null);

            Assert.Equal("/server/appa/assets/img/Logo.png", application.Icon.ToString());
            Assert.Equal(2, updated.Count);
        }

        /// <summary>
        /// Test that an unknown application context is ignored rather than throwing.
        /// </summary>
        [Fact]
        public void SetApplicationNameOfUnknownApplication()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationManager = componentHub.ApplicationManager;
            var updated = 0;

            applicationManager.UpdateApplication += (_, _) => updated++;

            // act
            applicationManager.SetApplicationName(null, "Renamed");

            // validation
            Assert.Equal(0, updated);
        }

        /// <summary>
        /// Test the context path property of the application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/server/appa")]
        [InlineData(typeof(TestApplicationB), "/server/appb")]
        [InlineData(typeof(TestApplicationC), "/server")]
        public void ContextPath(Type applicationType, string contextPath)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            Assert.Equal(contextPath, application.Route.ToString());
        }

        /// <summary>
        /// Test the asset path property of the application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/asseta")]
        [InlineData(typeof(TestApplicationB), "/assetb")]
        [InlineData(typeof(TestApplicationC), "*/")]
        public void AssetPath(Type applicationType, string assetPath)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            AssertExtensions.EqualWithPlaceholders(assetPath, application.AssetPath);
        }

        /// <summary>
        /// Test the data path property of the application.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), "/dataa")]
        [InlineData(typeof(TestApplicationB), "/datab")]
        [InlineData(typeof(TestApplicationC), "*/")]
        public void DataPath(Type applicationType, string dataPath)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            AssertExtensions.EqualWithPlaceholders(dataPath, application.DataPath);
        }

        /// <summary>
        /// Tests that the default theme declared via <c>[Theme&lt;TestThemeA&gt;]</c>
        /// resolves through the lazy <c>ApplicationContext.DefaultTheme</c>
        /// once the ThemeManager has registered the matching theme.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeA), "webexpress.webcore.test.testthemea")]
        [InlineData(typeof(TestApplicationB), null, null)]
        [InlineData(typeof(TestApplicationC), null, null)]
        public void DefaultTheme(Type applicationType, Type expectedThemeType, string expectedThemeId)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var theme = application?.DefaultTheme;

            // validation
            if (expectedThemeType is null)
            {
                Assert.Null(theme);
                return;
            }
            Assert.NotNull(theme);
            Assert.Equal(expectedThemeId, theme.ThemeId?.ToString());
        }

        /// <summary>
        /// Tests whether the application manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.ApplicationManager.GetType()));
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
            foreach (var application in componentHub.ApplicationManager.Applications)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(application.GetType()), $"Application context {application.GetType().Name} does not implement IContext.");
            }
        }
    }
}
