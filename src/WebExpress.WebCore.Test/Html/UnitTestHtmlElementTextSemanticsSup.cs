using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsSup class.
    /// </summary>
    public class UnitTestHtmlElementTextSemanticsSup
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsSup();

            Assert.Equal(@"<sup></sup>", html.Trim());
        }
    }
}
