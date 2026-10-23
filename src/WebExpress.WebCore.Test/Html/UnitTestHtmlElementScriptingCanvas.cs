using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementScriptingCanvas class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementScriptingCanvas
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementScriptingCanvas();

            Assert.Equal(@"<canvas></canvas>", html.Trim());
        }
    }
}
