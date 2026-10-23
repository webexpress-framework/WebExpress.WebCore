using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElement class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElement
    {
        /// <summary>
        /// Tests the find method.
        /// </summary>
        [Fact]
        public void FindSingel()
        {
            // arrange
            var html = new HtmlElementTextContentDiv
            (
                new HtmlElementTextSemanticsI(),
                new HtmlElementTextSemanticsU(new HtmlElementTextSemanticsSpan()),
                new HtmlElementTextSemanticsB()
            );

            // act
            var res = html.Find(x => x is HtmlElementTextSemanticsSpan).FirstOrDefault();

            // validation
            Assert.Equal(@"<span></span>", res.Trim());
        }

        /// <summary>
        /// Tests the find method.
        /// </summary>
        [Fact]
        public void Find()
        {
            // arrange
            var html = new HtmlElement[]
            {
                    new HtmlElementTextContentDiv
                    (
                        new HtmlElementTextSemanticsI(),
                        new HtmlElementTextSemanticsU(new HtmlElementTextSemanticsSpan()),
                        new HtmlElementTextSemanticsB()
                    ),
                    new HtmlElementMultimediaImg()
            };

            // act
            var res = html.Find(x => x is HtmlElementTextSemanticsSpan).FirstOrDefault();

            // validation
            Assert.Equal(@"<span></span>", res.Trim());
        }

        /// <summary>
        /// Tests the AddClass method.
        /// </summary>
        [Fact]
        public void AddClassTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();

            // act
            div.AddClass("test-class");

            // validation
            Assert.Contains("class=\"test-class\"", div.ToString());
        }

        /// <summary>
        /// Tests the RemoveClass method.
        /// </summary>
        [Fact]
        public void RemoveClassTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();
            div.AddClass("test-class");

            // act
            div.RemoveClass("test-class");

            // validation
            Assert.DoesNotContain("class=\"test-class\"", div.ToString());
        }

        /// <summary>
        /// Tests the AddStyle method.
        /// </summary>
        [Fact]
        public void AddStyleTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();

            // act
            div.AddStyle("color:red;");

            // validation
            Assert.Contains("style=\"color:red;\"", div.ToString());
        }

        /// <summary>
        /// Tests the RemoveStyle method.
        /// </summary>
        [Fact]
        public void RemoveStyleTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();
            div.AddStyle("color", "red");

            // act
            div.RemoveStyle("color");

            // validation
            Assert.DoesNotContain("style=\"color:red;\"", div.ToString());
        }

        /// <summary>
        /// Tests adding multiple CSS classes.
        /// </summary>
        [Fact]
        public void AddMultipleClassesTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();

            // act
            div.AddClass("class1");
            div.AddClass("class2");

            // validation
            Assert.Contains("class=\"class1 class2\"", div.ToString());
        }

        /// <summary>
        /// Tests removing one of multiple CSS classes.
        /// </summary>
        [Fact]
        public void RemoveOneOfMultipleClassesTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();
            div.AddClass("class1");
            div.AddClass("class2");

            // act
            div.RemoveClass("class1");

            // validation
            Assert.DoesNotContain("class1", div.ToString());
            Assert.Contains("class2", div.ToString());
        }

        /// <summary>
        /// Tests adding multiple styles.
        /// </summary>
        [Fact]
        public void AddMultipleStylesTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();

            // act
            div.AddStyle("color:red;");
            div.AddStyle("background:blue;");

            // validation
            Assert.Contains("color:red;", div.ToString());
            Assert.Contains("background:blue;", div.ToString());
        }

        /// <summary>
        /// Tests removing one of multiple styles.
        /// </summary>
        [Fact]
        public void RemoveOneOfMultipleStylesTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();
            div.AddStyle("color:red;");
            div.AddStyle("background:blue;");

            // act
            div.RemoveStyle("color:red;");

            // validation
            Assert.DoesNotContain("color:red;", div.ToString());
            Assert.Contains("background:blue;", div.ToString());
        }

        /// <summary>
        /// Tests that ToString returns the correct HTML for an empty div.
        /// </summary>
        [Fact]
        public void ToStringEmptyDivTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv();

            // validation
            Assert.Equal("<div></div>", div.ToString().Trim());
        }

        /// <summary>
        /// Tests that ToString returns the correct HTML for a div with child elements.
        /// </summary>
        [Fact]
        public void ToStringWithChildrenTest()
        {
            // arrange
            var div = new HtmlElementTextContentDiv(
                new HtmlElementTextSemanticsB(),
                new HtmlElementTextSemanticsI()
            );

            // validation
            Assert.Contains("<b></b>", div.ToString());
            Assert.Contains("<i></i>", div.ToString());
        }

        /// <summary>
        /// Tests that formatting inside running text is written without a blank around it,
        /// so a word formatted in part stays one word, while block elements keep their line.
        /// </summary>
        [Fact]
        public void BreakNeverTouchesText()
        {
            // arrange
            var p = new HtmlElementTextContentP
            (
                new HtmlText("x"),
                new HtmlElementTextSemanticsStrong(new HtmlText("b"), new HtmlElementTextSemanticsEm(new HtmlText("i"))),
                new HtmlText("y "),
                new HtmlElementTextSemanticsA(new HtmlText("link")) { Href = "/l" },
                new HtmlText(", z"),
                new HtmlElementTextSemanticsStrong(new HtmlElementTextSemanticsEm(new HtmlText("w"))),
                new HtmlText(".")
            );
            var div = new HtmlElementTextContentDiv(p);

            // act
            var html = div.ToString();

            // validation
            Assert.Contains(@"<p>x<strong>b<em>i</em></strong>y ", html);
            Assert.Contains(@"link</a>, z<strong><em>w</em></strong>.", html);
            Assert.Matches(@"<div>\r?\n\s*<p>", html);
        }

        /// <summary>
        /// Tests that two neighbouring elements keep the break between them, which renders as
        /// the blank that controls rely on between an icon and its label.
        /// </summary>
        [Fact]
        public void BreakBetweenElementsIsKept()
        {
            // arrange
            var link = new HtmlElementTextSemanticsA
            (
                new HtmlElementTextSemanticsI() { Class = "icon" },
                new HtmlElementTextSemanticsSpan(new HtmlText("Label"))
            );

            // act
            var html = link.ToString();

            // validation
            Assert.Matches(@"<a><i class=""icon""></i>\r?\n\s*<span>Label</span></a>", html);
        }

    }
}
