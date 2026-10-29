using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMetadataStyle class.
    /// </summary>
    public class UnitTestHtmlElementMetadataStyle
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMetadataStyle();

            Assert.Equal(@"<style></style>", html.Trim());
        }
    }
}
