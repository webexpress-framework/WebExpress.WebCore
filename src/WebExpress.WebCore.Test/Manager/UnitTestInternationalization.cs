using System.Globalization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the internationalization manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestInternationalization
    {
        /// <summary>
        /// Test the register function of the internationalization manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;

            // act
            pluginManager.Register();

            Assert.Equal("This is a test", I18N.Translate("webexpress.webcore.test:unit.test.message"));
        }

        /// <summary>
        /// Test the remove function of the internationalization manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var internationalizationManager = componentHub.InternationalizationManager as InternationalizationManager;
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            internationalizationManager.Remove(plugin);

            Assert.Equal("webexpress.webcore.test:unit.test.message", I18N.Translate("webexpress.webcore.test:unit.test.message"));
        }

        /// <summary>
        /// Test the default culture property of the internationalization manager.
        /// </summary>
        [Fact]
        public void GetDefaultCulture()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(CultureInfo.GetCultureInfo("en"), InternationalizationManager.DefaultCulture);
        }

        /// <summary>
        /// Test the translate function of the internationalization manager.
        /// </summary>
        [Theory]
        [InlineData("webexpress.webcore.test:unit.test.message", "This is a test")]
        [InlineData("webexpress.webcore.test:unit.test.message", "This is a test", "en")]
        [InlineData("webexpress.webcore.test:unit.test.message", "Dies ist ein Test", "de")]
        [InlineData("webexpress.webcore.test:unit.test.message", "Dies ist ein Test", "de", "webexpress.webcore.test")]
        [InlineData("webexpress.webcore.test:welcome.message", "Welcome 'Max' to our application!", "en", null, "Max")]
        [InlineData("welcome.message", "Welcome 'Max' to our application!", "en", "webexpress.webcore.test", "Max")]
        [InlineData("webexpress.webcore:app.startup", "Startup", "en", null)]
        [InlineData("non.existent.key", "non.existent.key", "de")]
        public void Translate(string key, string excepted, string cultureName = null, string pluginID = null, params object[] param)
        {
            // arrange
            UnitTestFixture.CreateAndRegisterComponentHubMock();

            if (cultureName is null && param.Length == 0)
            {
                // act
                var result = I18N.Translate(key);

                Assert.Equal(excepted, result);
            }
            if (cultureName is null && param.Length != 0)
            {
                // act
                var result = I18N.Translate(key, param);

                Assert.Equal(excepted, result);
            }
            if (cultureName is not null && pluginID is null && param.Length == 0)
            {
                // act
                var result = I18N.Translate(CultureInfo.GetCultureInfo(cultureName), key);

                Assert.Equal(excepted, result);
            }
            if (cultureName is not null && pluginID is null && param.Length != 0)
            {
                // act
                var result = I18N.Translate(CultureInfo.GetCultureInfo(cultureName), key, param);

                Assert.Equal(excepted, result);
            }
            if (cultureName is not null && pluginID is not null && param.Length == 0)
            {
                // act
                var result = I18N.Translate(CultureInfo.GetCultureInfo(cultureName), pluginID, key);

                Assert.Equal(excepted, result);
            }
            if (cultureName is not null && pluginID is not null && param.Length != 0)
            {
                // act
                var result = I18N.Translate(CultureInfo.GetCultureInfo(cultureName), pluginID, key, param);

                Assert.Equal(excepted, result);
            }
        }

        /// <summary>
        /// Tests whether the internationalization manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.InternationalizationManager.GetType()));
        }

        /// <summary>
        /// Tests that a key without a translation that holds a brace - keys are often built from
        /// data - is returned as it stands instead of failing the request in string.Format.
        /// </summary>
        [Fact]
        public void UntranslatedKeyWithBraceIsNoFormat()
        {
            // arrange
            _ = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            var result = I18N.Translate("status.{broken", "x");

            // validation
            Assert.Equal("status.{broken", result);
        }
    }
}
