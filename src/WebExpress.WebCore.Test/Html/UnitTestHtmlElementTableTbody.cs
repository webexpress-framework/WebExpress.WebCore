using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableTbody class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableTbody
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableTbody();

            Assert.Equal(@"<tbody></tbody>", html.Trim());
        }
    }
}
