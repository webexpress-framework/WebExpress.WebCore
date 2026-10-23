using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionNav class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionNav
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionNav();

            Assert.Equal(@"<nav></nav>", html.Trim());
        }
    }
}
