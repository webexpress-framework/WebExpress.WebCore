using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementInteractiveDialog class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementInteractiveDialog
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementInteractiveDialog();

            Assert.Equal(@"<dialog></dialog>", html.Trim());
        }
    }
}
