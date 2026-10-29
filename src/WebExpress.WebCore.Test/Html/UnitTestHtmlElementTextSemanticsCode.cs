using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsCode class.
    /// </summary>
    public class UnitTestHtmlElementTextSemanticsCode
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsCode();

            Assert.Equal(@"<code></code>", html.Trim());
        }
    }
}
