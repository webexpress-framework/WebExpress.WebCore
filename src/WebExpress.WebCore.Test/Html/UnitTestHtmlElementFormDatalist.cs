using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormDatalist class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormDatalist
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormDatalist();

            Assert.Equal(@"<datalist></datalist>", html.Trim());
        }
    }
}
