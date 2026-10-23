using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormOption class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormOption
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormOption();

            Assert.Equal(@"<option></option>", html.Trim());
        }
    }
}
