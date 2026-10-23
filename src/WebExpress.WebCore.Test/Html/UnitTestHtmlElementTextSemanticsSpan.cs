using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsSpan class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextSemanticsSpan
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsSpan();

            Assert.Equal(@"<span></span>", html.Trim());
        }
    }
}
