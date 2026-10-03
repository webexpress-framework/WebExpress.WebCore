using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextContentBlockquote class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextContentBlockquote
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextContentBlockquote();

            Assert.Equal(@"<blockquote></blockquote>", html.Trim());
        }
    }
}
