using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFormOptgroup class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFormOptgroup
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFormOptgroup();

            Assert.Equal(@"<optgroup></optgroup>", html.Trim());
        }
    }
}
