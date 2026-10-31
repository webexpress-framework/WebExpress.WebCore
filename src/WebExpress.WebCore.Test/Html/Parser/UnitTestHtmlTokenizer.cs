using System.Linq;
using WebExpress.WebCore.WebHtml.Parser;

namespace WebExpress.WebCore.Test.Html.Parser
{
    /// <summary>
    /// Unit tests for the <see cref="HtmlTokenizer"/> class.
    /// </summary>
    public class UnitTestHtmlTokenizer
    {
        // ------------------------------------------------------------------
        // Helper
        // ------------------------------------------------------------------

        private static HtmlToken[] Tokenize(string html)
        {
            var tokenizer = new HtmlTokenizer(html);
            return [.. tokenizer.Tokenize()];
        }

        // ------------------------------------------------------------------
        // Basic tokens
        // ------------------------------------------------------------------

        /// <summary>
        /// An empty input produces only an EOF token.
        /// </summary>
        [Fact]
        public void EmptyInput_ReturnsEof()
        {
            var tokens = Tokenize("");

            Assert.Single(tokens);
            Assert.Equal(HtmlTokenType.EndOfFile, tokens[0].Type);
        }

        /// <summary>
        /// A plain text input produces a text token.
        /// </summary>
        [Fact]
        public void PlainText_ReturnsTextToken()
        {
            var tokens = Tokenize("Hello World");

            Assert.Equal(HtmlTokenType.Text, tokens[0].Type);
            Assert.Equal("Hello World", tokens[0].Value);
        }

        /// <summary>
        /// A simple start tag produces a start-tag token.
        /// </summary>
        [Fact]
        public void SimpleStartTag_ReturnsStartTag()
        {
            var tokens = Tokenize("<div>");

            Assert.Equal(HtmlTokenType.StartTag, tokens[0].Type);
            Assert.Equal("div", tokens[0].TagName);
        }

        /// <summary>
        /// A simple end tag produces an end-tag token.
        /// </summary>
        [Fact]
        public void SimpleEndTag_ReturnsEndTag()
        {
            var tokens = Tokenize("</div>");

            Assert.Equal(HtmlTokenType.EndTag, tokens[0].Type);
            Assert.Equal("div", tokens[0].TagName);
        }

        /// <summary>
        /// An explicit self-closing tag produces a self-closing token.
        /// </summary>
        [Fact]
        public void ExplicitSelfClosingTag_ReturnsSelfClosing()
        {
            var tokens = Tokenize("<br/>");

            Assert.Equal(HtmlTokenType.SelfClosingTag, tokens[0].Type);
            Assert.Equal("br", tokens[0].TagName);
        }

        /// <summary>
        /// A void element without a trailing slash is still emitted as self-closing.
        /// </summary>
        [Fact]
        public void VoidElement_ReturnsSelfClosing()
        {
            var tokens = Tokenize("<img>");

            Assert.Equal(HtmlTokenType.SelfClosingTag, tokens[0].Type);
            Assert.Equal("img", tokens[0].TagName);
        }

        // ------------------------------------------------------------------
        // Attributes
        // ------------------------------------------------------------------

        /// <summary>
        /// Quoted attribute values are correctly extracted.
        /// </summary>
        [Fact]
        public void TagWithQuotedAttribute_ExtractsAttribute()
        {
            var tokens = Tokenize("<div class=\"foo\">");

            Assert.Equal(HtmlTokenType.StartTag, tokens[0].Type);
            var attr = tokens[0].Attributes.Single();
            Assert.Equal("class", attr.Name);
            Assert.Equal("foo", attr.Value);
        }

        /// <summary>
        /// Single-quoted attribute values are correctly extracted.
        /// </summary>
        [Fact]
        public void TagWithSingleQuotedAttribute_ExtractsAttribute()
        {
            var tokens = Tokenize("<div class='bar'>");

            var attr = tokens[0].Attributes.Single();
            Assert.Equal("class", attr.Name);
            Assert.Equal("bar", attr.Value);
        }

        /// <summary>
        /// Multiple attributes on a single tag are all extracted.
        /// </summary>
        [Fact]
        public void TagWithMultipleAttributes_ExtractsAll()
        {
            var tokens = Tokenize("<input id=\"x\" type=\"text\" disabled>");

            var attrs = tokens[0].Attributes;
            Assert.Equal(3, attrs.Count);
            Assert.Equal("id", attrs[0].Name);
            Assert.Equal("x", attrs[0].Value);
            Assert.Equal("type", attrs[1].Name);
            Assert.Equal("text", attrs[1].Value);
            Assert.Equal("disabled", attrs[2].Name);
            Assert.True(attrs[2].IsBoolean);
        }

        /// <summary>
        /// Boolean (valueless) attributes are represented without a value.
        /// </summary>
        [Fact]
        public void BooleanAttribute_IsBoolean()
        {
            var tokens = Tokenize("<input required>");

            var attr = tokens[0].Attributes.Single();
            Assert.True(attr.IsBoolean);
            Assert.Null(attr.Value);
        }

        /// <summary>
        /// Data attributes are preserved as-is.
        /// </summary>
        [Fact]
        public void DataAttribute_IsPreserved()
        {
            var tokens = Tokenize("<div data-toggle=\"modal\">");

            var attr = tokens[0].Attributes.Single();
            Assert.Equal("data-toggle", attr.Name);
            Assert.Equal("modal", attr.Value);
        }

        /// <summary>
        /// ARIA attributes are preserved as-is.
        /// </summary>
        [Fact]
        public void AriaAttribute_IsPreserved()
        {
            var tokens = Tokenize("<button aria-label=\"Close\"></button>");

            var attr = tokens[0].Attributes.Single();
            Assert.Equal("aria-label", attr.Name);
            Assert.Equal("Close", attr.Value);
        }

        // ------------------------------------------------------------------
        // Comments and DOCTYPE
        // ------------------------------------------------------------------

        /// <summary>
        /// An HTML comment produces a comment token whose value does not include the delimiters.
        /// </summary>
        [Fact]
        public void Comment_ReturnsCommentToken()
        {
            var tokens = Tokenize("<!-- remark -->");

            Assert.Equal(HtmlTokenType.Comment, tokens[0].Type);
            Assert.Equal("remark", tokens[0].Value);
        }

        /// <summary>
        /// A DOCTYPE declaration produces a doctype token.
        /// </summary>
        [Fact]
        public void Doctype_ReturnsDoctypeToken()
        {
            var tokens = Tokenize("<!DOCTYPE html>");

            Assert.Equal(HtmlTokenType.Doctype, tokens[0].Type);
            Assert.Equal("html", tokens[0].TagName);
        }

        // ------------------------------------------------------------------
        // Compound input
        // ------------------------------------------------------------------

        /// <summary>
        /// A typical HTML snippet produces the expected sequence of tokens.
        /// </summary>
        [Fact]
        public void CompoundInput_ProducesExpectedSequence()
        {
            var tokens = Tokenize("<p>Hello</p>");

            Assert.Equal(HtmlTokenType.StartTag, tokens[0].Type);
            Assert.Equal("p", tokens[0].TagName);

            Assert.Equal(HtmlTokenType.Text, tokens[1].Type);
            Assert.Equal("Hello", tokens[1].Value);

            Assert.Equal(HtmlTokenType.EndTag, tokens[2].Type);
            Assert.Equal("p", tokens[2].TagName);

            Assert.Equal(HtmlTokenType.EndOfFile, tokens[3].Type);
        }

        /// <summary>
        /// Tags names are normalised to lower case.
        /// </summary>
        [Fact]
        public void TagNameNormalisation_IsLowerCase()
        {
            var tokens = Tokenize("<DIV class=\"FOO\">");

            Assert.Equal("div", tokens[0].TagName);
            Assert.Equal("class", tokens[0].Attributes[0].Name);
        }

        // ------------------------------------------------------------------
        // Whitespace and edge cases
        // ------------------------------------------------------------------

        /// <summary>
        /// Whitespace-only text between tags is preserved as a text token.
        /// </summary>
        [Fact]
        public void WhitespaceText_IsPreservedAsTextToken()
        {
            var tokens = Tokenize("<div> </div>");

            Assert.Equal(HtmlTokenType.StartTag, tokens[0].Type);
            Assert.Equal(HtmlTokenType.Text, tokens[1].Type);
            Assert.Equal(" ", tokens[1].Value);
            Assert.Equal(HtmlTokenType.EndTag, tokens[2].Type);
        }

        /// <summary>
        /// An unquoted attribute value is read until whitespace or closing bracket.
        /// </summary>
        [Fact]
        public void UnquotedAttributeValue_IsExtracted()
        {
            var tokens = Tokenize("<div class=foo>");

            var attr = tokens[0].Attributes.Single();
            Assert.Equal("class", attr.Name);
            Assert.Equal("foo", attr.Value);
        }

        /// <summary>
        /// An inline style attribute is preserved in its entirety.
        /// </summary>
        [Fact]
        public void InlineStyleAttribute_IsPreserved()
        {
            var tokens = Tokenize("<div style=\"color: red; font-size: 14px;\">");

            var attr = tokens[0].Attributes.Single();
            Assert.Equal("style", attr.Name);
            Assert.Equal("color: red; font-size: 14px;", attr.Value);
        }

        /// <summary>
        /// A stray less-than character is emitted as text.
        /// </summary>
        [Fact]
        public void StrayLessThan_IsEmittedAsText()
        {
            var tokens = Tokenize("a < b");

            Assert.Equal(HtmlTokenType.Text, tokens[0].Type);
            Assert.Equal("a ", tokens[0].Value);
            Assert.Equal(HtmlTokenType.Text, tokens[1].Type);
            Assert.Equal("<", tokens[1].Value);
            Assert.Equal(HtmlTokenType.Text, tokens[2].Type);
        }

        /// <summary>
        /// A keygen void element without slash is emitted as self-closing.
        /// </summary>
        [Fact]
        public void KeygenVoidElement_ReturnsSelfClosing()
        {
            var tokens = Tokenize("<keygen>");

            Assert.Equal(HtmlTokenType.SelfClosingTag, tokens[0].Type);
            Assert.Equal("keygen", tokens[0].TagName);
        }

        /// <summary>
        /// Keeps embedded markup literal until the matching raw text closing tag.
        /// </summary>
        /// <param name="tag">The raw text or escapable raw text element.</param>
        [Theory]
        [InlineData("script")]
        [InlineData("style")]
        [InlineData("title")]
        [InlineData("textarea")]
        [InlineData("iframe")]
        [InlineData("xmp")]
        [InlineData("noembed")]
        [InlineData("noframes")]
        public void RawText_OnlyRecognizesMatchingEndTag(string tag)
        {
            // arrange
            var content = $"<a href='https://example.com/'>literal</a></{tag}suffix>&amp;";
            var html = $"<{tag}>{content}</{tag.ToUpperInvariant()} ><p>after</p>";

            // act
            var tokens = Tokenize(html);

            // validation
            Assert.Equal(HtmlTokenType.Text, tokens[1].Type);
            Assert.Equal(content, tokens[1].Value);
            Assert.Equal(HtmlTokenType.EndTag, tokens[2].Type);
            Assert.Equal(tag, tokens[2].TagName);
            Assert.Equal("p", tokens[3].TagName);
            Assert.DoesNotContain(tokens, token => token.TagName == "a");
        }

        /// <summary>
        /// Resets raw text state when a tokenizer instance is reused after an incomplete document.
        /// </summary>
        [Fact]
        public void RawText_UnterminatedElementCanBeTokenizedRepeatedly()
        {
            // arrange
            var tokenizer = new HtmlTokenizer("<script><a>literal");

            // act
            var first = tokenizer.Tokenize();
            var second = tokenizer.Tokenize();

            // validation
            Assert.Equal(3, first.Count);
            Assert.Equal("<a>literal", first[1].Value);
            Assert.Equal(first.Select(token => token.ToString()), second.Select(token => token.ToString()));
        }

        /// <summary>
        /// Continues immediately after an empty raw text element.
        /// </summary>
        [Fact]
        public void RawText_EmptyElementPreservesFollowingMarkup()
        {
            // arrange
            const string html = "<script></script><a>link</a>";

            // act
            var tokens = Tokenize(html);

            // validation
            Assert.Equal(HtmlTokenType.EndTag, tokens[1].Type);
            Assert.Equal("a", tokens[2].TagName);
        }

        /// <summary>
        /// Treats all remaining plaintext content literally, including apparent closing tags.
        /// </summary>
        [Fact]
        public void Plaintext_DoesNotResumeMarkupParsing()
        {
            // arrange
            const string html = "<plaintext><a>literal</plaintext><p>still literal";

            // act
            var tokens = Tokenize(html);

            // validation
            Assert.Equal(3, tokens.Length);
            Assert.Equal("<a>literal</plaintext><p>still literal", tokens[1].Value);
        }
    }
}
