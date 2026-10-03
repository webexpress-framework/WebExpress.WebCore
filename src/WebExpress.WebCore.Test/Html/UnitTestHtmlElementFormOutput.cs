using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormOutput class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormOutput
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormOutput();

            Assert.Equal(@"<output></output>", html.Trim());
        }
    }
}
