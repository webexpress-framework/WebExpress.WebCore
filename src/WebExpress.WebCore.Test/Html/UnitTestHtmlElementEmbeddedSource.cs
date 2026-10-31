using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementEmbeddedSource class.
    /// </summary>
    public class UnitTestHtmlElementEmbeddedSource
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementEmbeddedSource();

            Assert.Equal(@"<source>", html.Trim());
        }
    }
}
