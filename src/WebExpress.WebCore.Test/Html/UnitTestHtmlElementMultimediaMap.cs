using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMultimediaMap class.
    /// </summary>
    public class UnitTestHtmlElementMultimediaMap
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMultimediaMap();

            Assert.Equal(@"<map></map>", html.Trim());
        }
    }
}
