using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFieldSelect class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFieldSelect
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFieldSelect();

            Assert.Equal(@"<select></select>", html.Trim());
        }
    }
}
