using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementEmbeddedEmbed class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementEmbeddedEmbed
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementEmbeddedEmbed();

            Assert.Equal(@"<embed>", html.Trim());
        }
    }
}
