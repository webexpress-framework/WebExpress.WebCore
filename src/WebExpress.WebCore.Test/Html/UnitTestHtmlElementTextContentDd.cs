using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextContentDd class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextContentDd
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextContentDd();

            Assert.Equal(@"<dd></dd>", html.Trim());
        }
    }
}
