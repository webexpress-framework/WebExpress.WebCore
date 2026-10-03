using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMetadataLink class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementMetadataLink
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMetadataLink();

            Assert.Equal(@"<link>", html.Trim());
        }
    }
}
