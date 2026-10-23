using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementWebFragmentsSlot class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementWebFragmentsSlot
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementWebFragmentsSlot();

            Assert.Equal(@"<slot></slot>", html.Trim());
        }
    }
}
