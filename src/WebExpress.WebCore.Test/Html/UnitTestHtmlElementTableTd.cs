using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableTd class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableTd
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableTd();

            Assert.Equal(@"<td></td>", html.Trim());
        }
    }
}
