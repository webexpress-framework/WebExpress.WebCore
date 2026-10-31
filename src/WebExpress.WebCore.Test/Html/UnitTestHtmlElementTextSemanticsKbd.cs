using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextSemanticsKbd class.
    /// </summary>
    public class UnitTestHtmlElementTextSemanticsKbd
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextSemanticsKbd();

            Assert.Equal(@"<kbd></kbd>", html.Trim());
        }
    }
}
