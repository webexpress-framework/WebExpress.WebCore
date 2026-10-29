using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementTextContentP class.
    /// </summary>
    public class UnitTestHtmlElementTextContentP
    {
        /// <summary>
        /// Tests a empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementTextContentP();

            Assert.Equal(@"<p></p>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtInstancing()
        {
            // act
            var html = new HtmlElementTextContentP("abcdef");

            Assert.Equal(@"<p>abcdef</p>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtProperty()
        {
            // act
            var html = new HtmlElementTextContentP
            {
                Text = "abcdef"
            };

            Assert.Equal(@"<p>abcdef</p>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtHtmlText()
        {
            // act
            var html = new HtmlElementTextContentP(new HtmlText("abc"), new HtmlText("def"));
            var str = html.ToString();

            Assert.Equal(@"<p>abcdef</p>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextWithId()
        {
            // act
            var html = new HtmlElementTextContentP()
            {
                Id = "identity"
            };

            Assert.Equal(@"<p id=""identity""></p>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void Inline()
        {
            // act
            var html = new HtmlElementTextContentP();

            Assert.False(html.Inline);
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void CloseTag()
        {
            // act
            var html = new HtmlElementTextContentP();

            Assert.True(html.CloseTag);
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void Class()
        {
            // act
            var html = new HtmlElementTextContentP()
            {
                Class = "abc"
            };

            Assert.Equal(@"<p class=""abc""></p>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void Style()
        {
            // act
            var html = new HtmlElementTextContentP()
            {
                Style = "abc"
            };

            Assert.Equal(@"<p style=""abc""></p>", html.Trim());
        }
    }
}
