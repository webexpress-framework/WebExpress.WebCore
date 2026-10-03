using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMetadataHead class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementMetadataHead
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMetadataHead();

            Assert.Equal(@"<head></head>", html.Trim());
        }
    }
}
