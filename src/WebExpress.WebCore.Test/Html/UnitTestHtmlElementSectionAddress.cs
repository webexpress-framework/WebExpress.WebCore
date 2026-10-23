using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionAddress class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionAddress
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionAddress();

            Assert.Equal(@"<address></address>", html.Trim());
        }
    }
}
