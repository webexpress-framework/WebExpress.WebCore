using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableTh class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableTh
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableTh();

            Assert.Equal(@"<th></th>", html.Trim());
        }
    }
}
