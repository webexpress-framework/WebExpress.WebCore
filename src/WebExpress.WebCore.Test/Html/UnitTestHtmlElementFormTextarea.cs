using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormTextarea class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormTextarea
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormTextarea();

            Assert.Equal(@"<textarea></textarea>", html.Trim());
        }
    }
}
