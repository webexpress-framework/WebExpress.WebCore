using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementInteractiveMenu class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementInteractiveMenu
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementInteractiveMenu();

            Assert.Equal(@"<menu></menu>", html.Trim());
        }
    }
}
