using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsRuby class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextSemanticsRuby
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsRuby();

            Assert.Equal(@"<ruby></ruby>", html.Trim());
        }
    }
}
