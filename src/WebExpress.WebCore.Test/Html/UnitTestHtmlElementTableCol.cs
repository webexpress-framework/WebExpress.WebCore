using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableCol class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableCol
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableCol();

            Assert.Equal(@"<col>", html.Trim());
        }
    }
}
