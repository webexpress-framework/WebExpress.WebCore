using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMultimediaImg class.
    /// </summary>
    public class UnitTestHtmlImage
    {
        /// <summary>
        /// Tests a empty image.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMultimediaImg();

            Assert.Equal(@"<img>", html.ToString().Trim());
        }
    }
}
