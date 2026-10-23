using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionMain class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionMain
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionMain();

            Assert.Equal(@"<main></main>", html.Trim());
        }
    }
}
