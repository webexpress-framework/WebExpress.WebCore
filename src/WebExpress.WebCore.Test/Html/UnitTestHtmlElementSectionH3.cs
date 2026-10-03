using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionH3 class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionH3
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionH3();

            Assert.Equal(@"<h3></h3>", html.Trim());
        }
    }
}
