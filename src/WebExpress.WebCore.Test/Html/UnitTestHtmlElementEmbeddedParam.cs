using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementEmbeddedParam class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementEmbeddedParam
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementEmbeddedParam();

            Assert.Equal(@"<param>", html.Trim());
        }
    }
}
