using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableColgroup class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableColgroup
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableColgroup();

            Assert.Equal(@"<colgroup></colgroup>", html.Trim());
        }
    }
}
