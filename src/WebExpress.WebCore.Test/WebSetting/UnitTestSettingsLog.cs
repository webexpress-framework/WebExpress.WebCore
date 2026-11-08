using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.WebSetting
{
    /// <summary>
    /// Protects startup diagnostics from credential disclosure and ambiguous multiline values.
    /// </summary>
    public class UnitTestSettingsLog
    {
        /// <summary>
        /// Keeps routing values visible while masking credentials in nested configuration sections.
        /// </summary>
        /// <param name="key">The merged configuration path.</param>
        /// <param name="value">The configured value.</param>
        /// <param name="expected">The safe diagnostic representation.</param>
        [Theory]
        [InlineData("WebExpress:ExternalUri", "http://vila/", "http://vila/")]
        [InlineData("WebExpress:Endpoints:0:Uri", "http://0.0.0.0:8080/", "http://0.0.0.0:8080/")]
        [InlineData("WebExpress:ExternalUri", null, "(not configured)")]
        [InlineData("WebExpress:Endpoints:0:Password", "test-secret", "[redacted]")]
        [InlineData("WebExpress:Authentication:SigningKey", "test-secret", "[redacted]")]
        [InlineData("Plugins:example:ClientSecret", "test-secret", "[redacted]")]
        [InlineData("Plugins:example:ConnectionString", "test-secret", "[redacted]")]
        [InlineData("Plugins:example:Uri", "https://user:password@example.com/", "[redacted]")]
        [InlineData("Plugins:example:Label", "first\r\nsecond", "first\\r\\nsecond")]
        public void FormatValuePreservesDiagnostics(string key, string value, string expected)
        {
            // arrange
            var configurationKey = key;

            // act
            var result = SettingsLog.FormatValue(configurationKey, value);

            // validation
            Assert.Equal(expected, result);
        }
    }
}
