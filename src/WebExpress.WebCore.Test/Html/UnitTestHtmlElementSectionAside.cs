using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionAside class.
    /// </summary>
    public class UnitTestHtmlElementSectionAside
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionAside();

            Assert.Equal(@"<aside></aside>", html.Trim());
        }
    }
}
