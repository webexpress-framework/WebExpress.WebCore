using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebTheme;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the theme manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestThemeManager
    {
        /// <summary>
        /// Test the register function of the theme manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(6, componentHub.ThemeManager.Themes.Count());
        }

        /// <summary>
        /// Test the remove function of the theme manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var themeManager = componentHub.ThemeManager as ThemeManager;
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            themeManager.Remove(plugin);

            // validation
            Assert.Empty(componentHub.ThemeManager.Themes);
        }

        /// <summary>
        /// Tests whether the theme manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.ThemeManager.GetType()));
        }

        /// <summary>
        /// Test the id property of the theme.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeA), "webexpress.webcore.test.testthemea")]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeB), "webexpress.webcore.test.testthemeb")]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeA), "webexpress.webcore.test.testthemea")]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeB), "webexpress.webcore.test.testthemeb")]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeA), "webexpress.webcore.test.testthemea")]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeB), "webexpress.webcore.test.testthemeb")]
        public void Id(Type applicationType, Type themeType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var themes = componentHub.ThemeManager.GetThemes(application, themeType);

            // validation
            if (id is null)
            {
                Assert.Empty(themes);
                return;
            }

            Assert.Contains(id, themes.Select(x => x.ThemeId?.ToString()));
        }

        /// <summary>
        /// Test the name property of the theme.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeA), "TestThemeA")]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeB), "TestThemeB")]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeA), "TestThemeA")]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeB), "TestThemeB")]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeA), "TestThemeA")]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeB), "TestThemeB")]
        public void Name(Type applicationType, Type themeType, string name)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var themes = componentHub.ThemeManager.GetThemes(application, themeType);

            // validation
            if (name is null)
            {
                Assert.Empty(themes);
                return;
            }

            Assert.Contains(name, themes?.Select(x => x.Name));
        }

        /// <summary>
        /// Test the description property of the theme.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeA), "A dummy theme for testing.")]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeB), null)]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeA), "A dummy theme for testing.")]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeB), null)]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeA), "A dummy theme for testing.")]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeB), null)]
        public void Description(Type applicationType, Type themeType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var theme = componentHub.ThemeManager.GetThemes(application, themeType).FirstOrDefault();

            // validation
            Assert.NotNull(theme);
            Assert.Equal(expected, theme?.Description);
        }

        /// <summary>
        /// Test the image property of the theme.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeA), "/server/appa/webexpress.webcore.test.testthemea.png")]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeB), null)]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeA), "/server/appb/webexpress.webcore.test.testthemea.png")]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeB), null)]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeA), "/server/webexpress.webcore.test.testthemea.png")]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeB), null)]
        public void Image(Type applicationType, Type themeType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var theme = componentHub.ThemeManager.GetThemes(application, themeType).FirstOrDefault();

            // validation
            Assert.NotNull(theme);
            Assert.Equal(expected, theme?.Image?.ToString());
        }

        /// <summary>
        /// Test the theme mode property of the theme.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeA), ThemeMode.Dark)]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeB), ThemeMode.Light)]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeA), ThemeMode.Dark)]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeB), ThemeMode.Light)]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeA), ThemeMode.Dark)]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeB), ThemeMode.Light)]
        public void Mode(Type applicationType, Type themeType, ThemeMode expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var theme = componentHub.ThemeManager.GetThemes(application, themeType).FirstOrDefault();

            // validation
            Assert.NotNull(theme);
            Assert.Equal(expected, theme?.ThemeMode);
        }

        /// <summary>
        /// Test the theme style property of the theme.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeA), "/server/appa/asserts/css/themea.css")]
        [InlineData(typeof(TestApplicationA), typeof(TestThemeB), null)]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeA), "/server/appb/asserts/css/themea.css")]
        [InlineData(typeof(TestApplicationB), typeof(TestThemeB), null)]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeA), "/server/asserts/css/themea.css")]
        [InlineData(typeof(TestApplicationC), typeof(TestThemeB), null)]
        public void ThemeStyle(Type applicationType, Type themeType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var theme = componentHub.ThemeManager.GetThemes(application, themeType).FirstOrDefault();

            // validation
            Assert.NotNull(theme);
            Assert.Equal(expected, theme?.ThemeStyle?.ToString());
        }
    }
}
