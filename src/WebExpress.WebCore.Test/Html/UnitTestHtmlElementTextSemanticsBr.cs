using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsBr class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextSemanticsBr
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsBr();

            Assert.Equal(@"<br>", html.Trim());
        }
    }
}
