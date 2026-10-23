using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextContentFigcaption class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextContentFigcaption
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextContentFigcaption();

            Assert.Equal(@"<figcaption></figcaption>", html.Trim());
        }
    }
}
