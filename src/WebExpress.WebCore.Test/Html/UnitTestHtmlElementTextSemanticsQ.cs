using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsQ class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextSemanticsQ
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsQ();

            Assert.Equal(@"<q></q>", html.Trim());
        }
    }
}
