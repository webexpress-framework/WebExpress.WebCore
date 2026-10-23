using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementEmbeddedObject class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementEmbeddedObject
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementEmbeddedObject();

            Assert.Equal(@"<object></object>", html.Trim());
        }
    }
}
