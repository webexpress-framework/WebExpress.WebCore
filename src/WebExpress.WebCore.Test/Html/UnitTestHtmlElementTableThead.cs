using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTableThead class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementTableThead
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTableThead();

            Assert.Equal(@"<thead></thead>", html.Trim());
        }
    }
}
