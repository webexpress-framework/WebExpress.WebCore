using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionSearch class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementSectionSearch
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionSearch();

            Assert.Equal(@"<search></search>", html.Trim());
        }
    }
}
