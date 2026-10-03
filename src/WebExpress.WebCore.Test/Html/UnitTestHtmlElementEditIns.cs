using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementEditIns class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementEditIns
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementEditIns();

            Assert.Equal(@"<ins></ins>", html.Trim());
        }
    }
}
