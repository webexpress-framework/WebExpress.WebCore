using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionH1 class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionH1
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionH1();

            Assert.Equal(@"<h1></h1>", html.Trim());
        }
    }
}
