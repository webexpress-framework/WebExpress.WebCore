using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMultimediaMath class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementMultimediaMath
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMultimediaMath();

            Assert.Equal(@"<math></math>", html.Trim());
        }
    }
}
