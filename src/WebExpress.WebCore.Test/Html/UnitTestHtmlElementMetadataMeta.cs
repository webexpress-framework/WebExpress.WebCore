using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMetadataMeta class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementMetadataMeta
    {
        /// <summary>
        /// Tests the one key that names a declaration by itself and therefore
        /// carries its value directly.
        /// </summary>
        [Fact]
        public void KeyValue()
        {
            // act
            var html = new HtmlElementMetadataMeta("charset", "utf-8");

            Assert.Equal(@"<meta charset='utf-8'>", html.Trim());
        }

        /// <summary>
        /// Tests that every other key becomes a name/content pair. Rendered as a
        /// single attribute the declaration is markup no browser acts on, which
        /// is what left the viewport without effect on small screens.
        /// </summary>
        [Theory]
        [InlineData("viewport", "width=device-width, initial-scale=1")]
        [InlineData("description", "A page.")]
        public void NamedValue(string key, string value)
        {
            // act
            var html = new HtmlElementMetadataMeta(key, value);

            Assert.Equal($"<meta name='{key}' content='{value}'>", html.Trim());
        }
    }
}
