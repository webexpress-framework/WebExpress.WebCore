using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsS class.
    /// </summary>
    public class UnitTestHtmlElementTextSemanticsS
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsS();

            Assert.Equal(@"<s></s>", html.Trim());
        }
    }
}
