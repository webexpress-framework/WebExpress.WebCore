using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionFooter class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionFooter
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionFooter();

            Assert.Equal(@"<footer></footer>", html.Trim());
        }
    }
}
