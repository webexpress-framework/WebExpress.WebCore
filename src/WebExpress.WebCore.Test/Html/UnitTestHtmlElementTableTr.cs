using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableTr class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableTr
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableTr();

            Assert.Equal(@"<tr></tr>", html.Trim());
        }
    }
}
