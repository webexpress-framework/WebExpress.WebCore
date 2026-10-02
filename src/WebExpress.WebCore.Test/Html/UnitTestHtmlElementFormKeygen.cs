using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormKeygen class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormKeygen
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormKeygen();

            Assert.Equal(@"<keygen>", html.Trim());
        }
    }
}
