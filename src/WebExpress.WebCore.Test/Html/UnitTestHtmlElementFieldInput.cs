using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFieldInput class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFieldInput
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFieldInput();

            // validation
            Assert.Equal(@"<input>", html.Trim());
        }
    }
}
