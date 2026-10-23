using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormProgress class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormProgress
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormProgress();

            Assert.Equal(@"<progress></progress>", html.Trim());
        }
    }
}
