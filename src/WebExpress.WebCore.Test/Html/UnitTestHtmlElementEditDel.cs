using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementEditDel class.
    /// </summary>
    public class UnitTestHtmlElementEditDel
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementEditDel();

            Assert.Equal(@"<del></del>", html.Trim());
        }
    }
}
