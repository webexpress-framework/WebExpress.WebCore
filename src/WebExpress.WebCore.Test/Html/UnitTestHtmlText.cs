using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlText class.
    /// </summary>
    public class UnitTestHtmlText
    {
        /// <summary>
        /// Tests a empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlText();

            Assert.Null(html.Value);
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtInstancing()
        {
            // act
            var html = new HtmlText("abcdef");

            Assert.Equal(@"abcdef", html.Value);
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtProperty()
        {
            // act
            var html = new HtmlText
            {
                Value = "abcdef"
            };

            Assert.Equal(@"abcdef", html.Value);
        }
    }
}
