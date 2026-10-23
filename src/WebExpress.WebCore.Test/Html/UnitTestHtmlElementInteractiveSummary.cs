using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementInteractiveSummary class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementInteractiveSummary
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementInteractiveSummary();

            Assert.Equal(@"<summary></summary>", html.Trim());
        }
    }
}
