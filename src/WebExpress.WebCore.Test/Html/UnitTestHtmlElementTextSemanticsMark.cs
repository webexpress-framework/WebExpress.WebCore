using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsMark class.
    /// </summary>
    public class UnitTestHtmlElementTextSemanticsMark
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsMark();

            Assert.Equal(@"<mark></mark>", html.Trim());
        }
    }
}
