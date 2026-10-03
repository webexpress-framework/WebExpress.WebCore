using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionBody class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionBody
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionBody();

            Assert.Equal(@"<body></body>", html.Trim());
        }
    }
}
