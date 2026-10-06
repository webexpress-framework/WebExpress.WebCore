using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFieldLegend class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFieldLegend
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFieldLegend();

            Assert.Equal(@"<legend></legend>", html.Trim());
        }
    }
}
