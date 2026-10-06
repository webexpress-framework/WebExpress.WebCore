using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFieldButton class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFieldButton
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFieldButton();

            Assert.Equal(@"<button></button>", html.Trim());
        }
    }
}
