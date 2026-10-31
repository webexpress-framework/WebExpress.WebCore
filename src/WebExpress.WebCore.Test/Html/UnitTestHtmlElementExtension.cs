using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the UnitTestHtmlElementExtension class.
    /// </summary>
    public class UnitTestHtmlElementExtension
    {
        /// <summary>
        /// Tests the AddClass method.
        /// </summary>
        [Theory]
        [InlineData("existing1 existing2", "new", "existing1 existing2 new")]
        [InlineData("existing", "new", "existing new")]
        [InlineData("existing", "new new", "existing new")]
        [InlineData("existing", "new1 new2", "existing new1 new2")]
        [InlineData("existing", "", "existing")]
        [InlineData("existing", null, "existing")]
        [InlineData("", "new", "new")]
        [InlineData(null, "new", "new")]
        public void AddClass(string initial, string toAdd, string expected)
        {
            // arrange
            var element = new HtmlElementTextContentDiv { Class = initial } as IHtmlNode;

            // act
            element.AddClass(toAdd);

            Assert.Equal(expected, (element as IHtmlElement).Class);
        }

        /// <summary>
        /// Tests the RemoveClass method.
        /// </summary>
        [Theory]
        [InlineData("one two three", null, "one two three")]
        [InlineData("one two three", "", "one two three")]
        [InlineData("one two three", "two", "one three")]
        [InlineData("alpha beta", "gamma", "alpha beta")]
        [InlineData(null, "gamma", "")]
        public void RemoveClass(string initial, string toRemove, string expected)
        {
            // arrange
            var element = new HtmlElementTextContentDiv { Class = initial } as IHtmlNode;

            // act
            element.RemoveClass(toRemove);

            // validation
            Assert.Equal(expected, (element as IHtmlElement).Class);
        }

        /// <summary>
        /// Tests the AddStyle method.
        /// </summary>
        [Theory]
        [InlineData("color:red;", "margin:10px;", "color:red; margin:10px;")]
        [InlineData("", "padding:5px;", "padding:5px;")]
        [InlineData("existing1 existing2", "new", "existing1 existing2 new")]
        [InlineData("existing", "new", "existing new")]
        [InlineData("existing", "new new", "existing new")]
        [InlineData("existing", "new1 new2", "existing new1 new2")]
        [InlineData("existing", "", "existing")]
        [InlineData("existing", null, "existing")]
        [InlineData("", "new", "new")]
        [InlineData(null, "new", "new")]
        public void AddStyle(string initial, string toAdd, string expected)
        {
            // arrange
            var element = new HtmlElementTextContentDiv { Style = initial } as IHtmlNode;

            // act
            element.AddStyle(toAdd);

            // validation
            Assert.Equal(expected, (element as IHtmlElement).Style);
        }

        /// <summary>
        /// Tests the RemoveStyle method.
        /// </summary>
        [Theory]
        [InlineData("color:red; margin:10px;", "margin:10px;", "color:red;")]
        [InlineData("padding:5px;", "padding:5px;", "")]
        [InlineData("one two three", null, "one two three")]
        [InlineData("one two three", "", "one two three")]
        [InlineData("one two three", "two", "one three")]
        [InlineData("alpha beta", "gamma", "alpha beta")]
        [InlineData(null, "gamma", "")]
        public void RemoveStyle(string initial, string toRemove, string expected)
        {
            // arrange
            var element = new HtmlElementTextContentDiv { Style = initial } as IHtmlNode;

            // act
            element.RemoveStyle(toRemove);

            // validation
            Assert.Equal(expected.Trim(), (element as IHtmlElement).Style.Trim());
        }

        /// <summary>
        /// Tests the AddUserAttribute method.
        /// </summary>
        [Theory]
        [InlineData("data-test", null, "data-test")]
        [InlineData("data-id", "42", "data-id=42")]
        public void AddUserAttribute(string name, string value, string expected)
        {
            // arrange
            var element = new HtmlElementTextContentDiv() as IHtmlNode;

            // act
            if (!string.IsNullOrWhiteSpace(value))
            {
                element.AddUserAttribute(name, value);
            }
            else
            {
                element.AddUserAttribute(name);
            }

            // validation
            var attribute = (element as IHtmlElement).Attributes
                .Where(x => x.Name.Equals(name))
                .FirstOrDefault();

            if (attribute is HtmlAttribute valueAttribute)
            {
                Assert.Equal(expected, $"{attribute?.Name}={valueAttribute?.Value}");
            }
            else
            {
                Assert.Equal(expected, attribute?.Name);
            }
        }

        /// <summary>
        /// Tests the RemoveUserAttribute method.
        /// </summary>
        [Theory]
        [InlineData("data-test", null)]
        [InlineData("data-id", "42")]
        public void RemoveUserAttribute(string name, string value)
        {
            // arrange
            var element = new HtmlElementTextContentDiv() as IHtmlNode;
            element.AddUserAttribute(name, value);

            // act
            element.RemoveUserAttribute(name);

            // validation
            Assert.False((element as IHtmlElement).Attributes.Where(x => x.Equals(name)).Any());
        }

        /// <summary>
        /// Tests the Find method.
        /// </summary>
        [Fact]
        public void Find()
        {
            // arrange
            var root = new HtmlElementTextContentDiv();
            var node = root as IHtmlNode;
            var child1 = new HtmlElementTextSemanticsSpan();
            var child2 = new HtmlElementTextSemanticsSpan();
            root.Add(child1);
            root.Add(child2);

            // act
            var result = node.Find(e => e is HtmlElementTextSemanticsSpan).ToList();

            // validation
            Assert.Equal(2, result.Count);
            Assert.Contains(child1, result);
            Assert.Contains(child2, result);
        }

        /// <summary>
        /// Tests the Find method.
        /// </summary>
        [Fact]
        public void FindOnCollection()
        {
            var div1 = new HtmlElementTextContentDiv();
            var span1 = new HtmlElementTextSemanticsSpan();
            div1.Add(span1);

            var div2 = new HtmlElementTextContentDiv();
            var span2 = new HtmlElementTextSemanticsSpan();
            div2.Add(span2);

            var nodes = new List<IHtmlNode> { div1, div2 } as IEnumerable<IHtmlNode>;
            var result = nodes.Find(e => e is HtmlElementTextSemanticsSpan);

            Assert.Equal(2, result.Count());
            Assert.Contains(span1, result);
            Assert.Contains(span2, result);
        }
    }
}
