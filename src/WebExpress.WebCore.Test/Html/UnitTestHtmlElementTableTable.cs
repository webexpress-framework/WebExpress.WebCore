using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableTable class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableTable
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableTable();

            Assert.Equal(@"<table><thead><tr></tr></thead><tbody></tbody></table>", html.Trim());
        }
    }
}
