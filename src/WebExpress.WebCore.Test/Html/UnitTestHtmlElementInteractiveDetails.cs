using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementInteractiveDetails class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementInteractiveDetails
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementInteractiveDetails();

            Assert.Equal(@"<details></details>", html.Trim());
        }
    }
}
