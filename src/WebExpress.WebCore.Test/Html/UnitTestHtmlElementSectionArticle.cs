using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionArticle class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionArticle
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionArticle();

            Assert.Equal(@"<article></article>", html.Trim());
        }
    }
}
