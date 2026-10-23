using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionHeader class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionHeader
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionHeader();

            Assert.Equal(@"<header></header>", html.Trim());
        }
    }
}
