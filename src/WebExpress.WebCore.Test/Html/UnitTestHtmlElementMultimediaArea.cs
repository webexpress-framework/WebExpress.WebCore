using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMultimediaArea class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementMultimediaArea
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMultimediaArea();

            Assert.Equal(@"<area>", html.Trim());
        }
    }
}
