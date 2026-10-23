using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionSection class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionSection
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionSection();

            Assert.Equal(@"<section></section>", html.Trim());
        }
    }
}
