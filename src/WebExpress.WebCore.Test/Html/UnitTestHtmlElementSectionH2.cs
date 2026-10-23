using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionH2 class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionH2
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionH2();

            Assert.Equal(@"<h2></h2>", html.Trim());
        }
    }
}
