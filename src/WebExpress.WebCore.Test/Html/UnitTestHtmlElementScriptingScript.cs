using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementScriptingScript class.
    /// </summary>
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

        /// <summary>
        /// Tests that a deferred script carries the attribute without a value, and loses it again.
        /// </summary>
        [Fact]
        public void Defer()
        {
            // arrange
            var html = new HtmlElementScriptingScript() { Src = "/a.js", Defer = true };

            // act
            var deferred = html.Trim();
            html.Defer = false;

            // validation
            Assert.Equal(@"<script type=""text/javascript"" src=""/a.js"" defer></script>", deferred);
            Assert.Equal(@"<script type=""text/javascript"" src=""/a.js""></script>", html.Trim());
        }
    }
}
