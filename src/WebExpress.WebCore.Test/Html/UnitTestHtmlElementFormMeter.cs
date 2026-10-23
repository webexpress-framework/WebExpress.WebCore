using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormMeter class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormMeter
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormMeter();

            Assert.Equal(@"<meter></meter>", html.Trim());
        }
    }
}
