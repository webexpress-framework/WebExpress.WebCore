using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMetadataBase class.
    /// </summary>
    public class UnitTestHtmlElementMetadataBase
    {
        /// <summary>
        /// Tests the constructor of the HtmlElementMetadataBase class.
        /// </summary>
        [Fact]
        public void Constructor()
        {
            // arrange
            var url = "https://example.com";

            // act
            var element = new HtmlElementMetadataBase(url);

            // validation
            Assert.Equal("https://example.com", element.Href);
        }

        /// <summary>
        /// Sets the URI for the HTML element and validates its format.
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("https://example.com", "https://example.com")]
        public void Href(string uri, string expected)
        {
            // arrange
            var element = new HtmlElementMetadataBase();

            // act
            element.Href = uri;

            // validation
            Assert.Equal(expected, element.Href);
        }
    }
}
