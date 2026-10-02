using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementScriptingNoscript class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementScriptingNoscript
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementScriptingNoscript();

            Assert.Equal(@"<noscript></noscript>", html.Trim());
        }
    }
}
