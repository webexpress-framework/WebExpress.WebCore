using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableCaption class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableCaption
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableCaption();

            Assert.Equal(@"<caption></caption>", html.Trim());
        }
    }
}
