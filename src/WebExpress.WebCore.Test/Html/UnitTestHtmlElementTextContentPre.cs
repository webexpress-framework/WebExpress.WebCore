using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextContentPre class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTextContentPre
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextContentPre();

            Assert.Equal(@"<pre></pre>", html.Trim());
        }
    }
}
