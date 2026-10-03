using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionH4 class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionH4
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionH4();

            Assert.Equal(@"<h4></h4>", html.Trim());
        }
    }
}
