using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementEmbeddedPicture class.
    /// </summary>
    public class UnitTestHtmlElementEmbeddedPicture
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementEmbeddedPicture();

            Assert.Equal(@"<picture></picture>", html.Trim());
        }
    }
}
