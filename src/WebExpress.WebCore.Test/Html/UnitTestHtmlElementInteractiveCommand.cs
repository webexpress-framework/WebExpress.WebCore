using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementInteractiveCommand class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementInteractiveCommand
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementInteractiveCommand();

            Assert.Equal(@"<command></command>", html.Trim());
        }
    }
}
