using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementScriptingScript class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementScriptingScript
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementScriptingScript();

            Assert.Equal(@"<script type=""text/javascript""></script>", html.Trim());
        }
    }
}
