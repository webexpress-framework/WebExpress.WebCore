using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMultimediaSvg class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementMultimediaSvg
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMultimediaSvg();

            Assert.Equal(@"<svg></svg>", html.Trim());
        }
    }
}
