using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsTime class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextSemanticsTime
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsTime();

            Assert.Equal(@"<time></time>", html.Trim());
        }
    }
}
