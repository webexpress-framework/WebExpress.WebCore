using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionHgroup class.
    /// </summary>
    public class UnitTestHtmlElementSectionHgroup
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionHgroup();

            Assert.Equal(@"<hgroup></hgroup>", html.Trim());
        }
    }
}
