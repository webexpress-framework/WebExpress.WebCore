using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionH6 class.
    /// </summary>
    public class UnitTestHtmlElementSectionH6
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionH6();

            Assert.Equal(@"<h6></h6>", html.Trim());
        }
    }
}
