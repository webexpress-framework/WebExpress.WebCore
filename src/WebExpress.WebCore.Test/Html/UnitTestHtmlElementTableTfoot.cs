using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableTfoot class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableTfoot
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableTfoot();

            Assert.Equal(@"<tfoot></tfoot>", html.Trim());
        }
    }
}
