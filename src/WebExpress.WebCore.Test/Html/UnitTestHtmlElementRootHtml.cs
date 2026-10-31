using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementRootHtml class.
    /// </summary>
    public class UnitTestHtmlElementRootHtml
    {
        /// <summary>
        /// Tests the Head.Base of the HtmlElementMetadataBase class.
        /// </summary>
        [Fact]
        public void HeadBase()
        {
            // arrange
            var url = "https://example.com";

            // act
            var element = new HtmlElementRootHtml();
            element.Head.Base = url;

            // validation
            Assert.Contains($"<base href=\"{url}\">", element.ToString());
        }
    }
}
