using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementFieldLabel class.
    /// </summary>
    public class UnitTestHtmlElementFieldLabel
    {
        /// <summary>
        /// Tests a empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementFieldLabel();

            Assert.Equal(@"<label></label>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtInstancing()
        {
            // act
            var html = new HtmlElementFieldLabel("abcdef");

            Assert.Equal(@"<label>abcdef</label>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtProperty()
        {
            // act
            var html = new HtmlElementFieldLabel
            {
                Text = "abcdef"
            };

            Assert.Equal(@"<label>abcdef</label>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextAtHtmlText()
        {
            // act
            var html = new HtmlElementFieldLabel(new HtmlText("abc"), new HtmlText("def"));

            Assert.Equal(@"<label>abcdef</label>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void TextWithId()
        {
            // act
            var html = new HtmlElementFieldLabel()
            {
                Id = "identity"
            };

            Assert.Equal(@"<label id=""identity""></label>", html.Trim());
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void Inline()
        {
            // act
            var html = new HtmlElementFieldLabel();

            Assert.False(html.Inline);
        }

        /// <summary>
        /// Tests a tag.
        /// </summary>
        [Fact]
        public void CloseTag()
        {
            // act
            var html = new HtmlElementFieldLabel();

            Assert.True(html.CloseTag);
        }

        /// <summary>
        /// Tests the class attribute.
        /// </summary>
        [Fact]
        public void Class()
        {
            // act
            var html = new HtmlElementFieldLabel()
            {
                Class = "abc"
            };

            Assert.Equal(@"<label class=""abc""></label>", html.Trim());
        }

        /// <summary>
        /// Tests the style attribute.
        /// </summary>
        [Fact]
        public void Style()
        {
            // act
            var html = new HtmlElementFieldLabel()
            {
                Style = "abc"
            };

            Assert.Equal(@"<label style=""abc""></label>", html.Trim());
        }
    }
}
