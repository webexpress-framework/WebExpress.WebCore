using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the escaping an attribute value goes through on its way into the markup.
    /// </summary>
    /// <remarks>
    /// The writer used to emit the value verbatim, so a value carrying a double quote ended its
    /// attribute early and the rest of it landed in the markup as stray attributes. Controls
    /// worked around it by encoding before handing the value over; the escaping belongs to the
    /// writer, and these tests pin it there.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlAttribute
    {
        /// <summary>
        /// Tests that a double quote cannot end the attribute early. This is the case json in a
        /// data attribute runs into, which is how a control hands structured state to its client.
        /// </summary>
        [Fact]
        public void EscapesTheDoubleQuote()
        {
            // arrange
            var html = new HtmlElementTextContentDiv()
                .AddUserAttribute("data-payload", @"{""object"":""SD-1""}");

            // act
            var res = html.ToString().Trim();

            // validation
            Assert.Equal(@"<div data-payload=""{&quot;object&quot;:&quot;SD-1&quot;}""></div>", res);
        }

        /// <summary>
        /// Tests that an ampersand cannot turn the text following it into an entity - a query
        /// string of <c>?a=1&amp;copy=2</c> would otherwise read a copyright sign.
        /// </summary>
        [Fact]
        public void EscapesTheAmpersand()
        {
            // arrange
            var html = new HtmlElementTextContentDiv()
                .AddUserAttribute("href", "/search?a=1&copy=2");

            // act
            var res = html.ToString().Trim();

            // validation
            Assert.Equal(@"<div href=""/search?a=1&amp;copy=2""></div>", res);
        }

        /// <summary>
        /// Tests that nothing else is escaped: the apostrophe, the angle brackets and a
        /// non-ascii character are all legal inside a double-quoted value on a utf-8 document,
        /// and escaping them would only make the markup harder to read.
        /// </summary>
        /// <param name="value">The attribute value under test.</param>
        [Theory]
        [InlineData("Guybrush's quest")]
        [InlineData("a < b > c")]
        [InlineData("Grüße aus Mêlée")]
        public void LeavesEverythingElseAlone(string value)
        {
            // arrange
            var html = new HtmlElementTextContentDiv()
                .AddUserAttribute("title", value);

            // act
            var res = html.ToString().Trim();

            // validation
            Assert.Equal($@"<div title=""{value}""></div>", res);
        }

        /// <summary>
        /// Tests that the standard attributes are escaped as well - they travel through the same
        /// writer, so a class is no more verbatim than a data attribute.
        /// </summary>
        [Fact]
        public void EscapesTheStandardAttributes()
        {
            // arrange
            var html = new HtmlElementTextContentDiv() { Class = @"a ""b"" c" };

            // act
            var res = html.ToString().Trim();

            // validation
            Assert.Equal(@"<div class=""a &quot;b&quot; c""></div>", res);
        }

        /// <summary>
        /// Tests that an empty value stays an empty attribute rather than disappearing into an
        /// exception.
        /// </summary>
        [Fact]
        public void EmptyValueIsWrittenAsAnEmptyAttribute()
        {
            // arrange
            var builder = new System.Text.StringBuilder();
            var attribute = new HtmlAttribute("data-empty", string.Empty);

            // act
            attribute.ToString(builder, 0);

            // validation
            Assert.Equal(@"data-empty=""""", builder.ToString());
        }
    }
}
