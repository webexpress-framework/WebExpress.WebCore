using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormFieldset class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormFieldset
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormFieldset();

            Assert.Equal(@"<fieldset></fieldset>", html.Trim());
        }
    }
}
