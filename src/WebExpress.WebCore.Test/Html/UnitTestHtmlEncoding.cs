using System.Text;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebHtml.Parser;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Tests that text written through the html classes cannot become markup, and that markup read
    /// by the parser is written back as the browser read it.
    /// </summary>
    public class UnitTestHtmlEncoding
    {
        private static readonly HtmlParser Parser = new();

        /// <summary>
        /// Renders nodes the way a page writes them.
        /// </summary>
        /// <param name="nodes">The nodes.</param>
        /// <returns>The markup.</returns>
        private static string Render(params IHtmlNode[] nodes)
        {
            var builder = new StringBuilder();
            foreach (var node in nodes)
            {
                node.ToString(builder, 0);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Verifies that a text node is encoded, alone and inside an element that holds only text.
        /// </summary>
        [Fact]
        public void TextIsEncoded()
        {
            // act
            var alone = Render(new HtmlText("<script>alert(1)</script> & co"));
            var inside = Render(new HtmlElementTextContentP("<img src=x onerror=alert(1)>"));

            // validation
            Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt; &amp; co", alone);
            Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", inside);
            Assert.DoesNotContain("<img", inside);
        }

        /// <summary>
        /// Verifies that markup the caller trusts is still written as it is through HtmlRaw.
        /// </summary>
        [Fact]
        public void RawIsWrittenAsItIs()
        {
            Assert.Equal("<b>x</b>", Render(new HtmlRaw("<b>x</b>")));
        }

        /// <summary>
        /// Verifies that the title of a page is encoded; a record a user named could close the
        /// title and open a script.
        /// </summary>
        [Fact]
        public void TitleIsEncoded()
        {
            // act
            var html = Render(new HtmlElementMetadataTitle("</title><script>alert(1)</script>"));

            // validation
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;/title&gt;", html);
        }

        /// <summary>
        /// Verifies that parsed text keeps its entities when it is written again, instead of being
        /// encoded twice or decoded into markup.
        /// </summary>
        /// <param name="html">The markup read.</param>
        /// <param name="expected">The markup expected back.</param>
        [Theory]
        [InlineData("<p>a &amp; b</p>", "<p>a &amp; b</p>")]
        [InlineData("<p>&lt;script&gt;</p>", "<p>&lt;script&gt;</p>")]
        [InlineData("<p>AT&T</p>", "<p>AT&amp;T</p>")]
        public void ParsedTextRoundTrips(string html, string expected)
        {
            Assert.Equal(expected, Render([.. Parser.Parse(html)]).Trim());
        }

        /// <summary>
        /// Verifies that the content of a raw text element is neither decoded nor encoded.
        /// </summary>
        [Fact]
        public void RawTextContentStaysRaw()
        {
            // act
            var html = Render([.. Parser.Parse("<xmp>a > b &amp; c</xmp>")]);

            // validation
            Assert.Contains("a > b &amp; c", html);
        }

        /// <summary>
        /// Verifies that a no-break space is no separator inside a tag. A browser reads
        /// "img src=x onerror=..." as the name of one inert element; splitting it here wrote
        /// an image with an event handler.
        /// </summary>
        [Fact]
        public void NoBreakSpaceIsNoSeparator()
        {
            // act
            var html = Render([.. Parser.Parse("<img src=x onerror=alert(1)>")]);

            // validation
            Assert.DoesNotContain("onerror=\"", html);
            Assert.DoesNotContain("<img ", html);
        }

        /// <summary>
        /// Verifies that a comment ends where a browser ends it, so markup after "--!&gt;" is parsed
        /// as markup and not hidden in a comment.
        /// </summary>
        [Fact]
        public void CommentEndsAtBangClose()
        {
            // act
            var nodes = Parser.Parse("<!-- a --!><b>after</b>");

            // validation
            Assert.Contains(nodes, x => x is HtmlElementTextSemanticsB);
        }

        /// <summary>
        /// Verifies that the text of a comment cannot end the comment it is written into.
        /// </summary>
        [Fact]
        public void CommentTextCannotEndTheComment()
        {
            // act
            var html = Render(new HtmlComment("x --><script>alert(1)</script>"));

            // validation
            Assert.DoesNotContain("--><script>", html);
            Assert.EndsWith(" -->", html);
            Assert.Equal(1, html.Split("-->").Length - 1);
        }

        /// <summary>
        /// Verifies that an attribute whose name could end the tag or open another attribute is not
        /// written.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        [Theory]
        [InlineData("a\"onmouseover")]
        [InlineData("x><script")]
        [InlineData("a b")]
        [InlineData("")]
        public void InvalidAttributeNamesAreNotWritten(string name)
        {
            // arrange
            var element = new HtmlElementTextContentDiv();
            element.AddUserAttribute(name, "v");
            element.AddUserAttribute("data-ok", "1");

            // act
            var html = Render(element);

            // validation
            Assert.Contains("data-ok=\"1\"", html);
            Assert.DoesNotContain("=\"v\"", html);
        }

        /// <summary>
        /// Verifies that a status message is text written encoded into the body.
        /// </summary>
        [Fact]
        public void StatusMessageIsEncoded()
        {
            // act
            var html = StatusMessage.ToHtml(new StatusMessage("bad <script>\nline"), "fallback");

            // validation
            Assert.Equal("<html><body><p>bad &lt;script&gt;<br/>line</p></body></html>", html);
            Assert.Equal("fallback", StatusMessage.ToHtml(null, "fallback"));
        }
    }
}
