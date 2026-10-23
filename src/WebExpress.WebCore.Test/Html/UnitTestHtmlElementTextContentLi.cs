using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextContentLi class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextContentLi
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextContentLi();

            Assert.Equal(@"<li></li>", html.Trim());
        }
    }
}
