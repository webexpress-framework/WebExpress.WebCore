using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMultimediaAudio class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementMultimediaAudio
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMultimediaAudio();

            Assert.Equal(@"<audio></audio>", html.Trim());
        }
    }
}
