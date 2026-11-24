using System.Diagnostics;
using System.Text;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebHtml.Parser;

namespace WebExpress.WebCore.Test.Html.Parser
{
    /// <summary>
    /// Tests that HTML from users cannot exhaust the stack or the processor of the server. A
    /// stack overflow cannot be caught in .NET and ended the whole process.
    /// </summary>
    public class UnitTestHtmlParserLimits
    {
        private static readonly HtmlParser Parser = new();

        /// <summary>
        /// Verifies that deeply nested elements parse into a bounded tree that renders.
        /// </summary>
        [Fact]
        public void DeepNestingIsBounded()
        {
            // arrange
            var html = string.Concat(Enumerable.Repeat("<div>", 200_000)) + "deep" + string.Concat(Enumerable.Repeat("</div>", 200_000));

            // act
            var nodes = Parser.Parse(html);
            var rendered = new StringBuilder();
            foreach (var node in nodes)
            {
                node.ToString(rendered, 0);
            }

            // validation
            Assert.True(Depth(nodes) <= HtmlParser.MaxNestingDepth + 1);
            Assert.Contains("deep", rendered.ToString());
        }

        /// <summary>
        /// Verifies that nesting below the limit keeps its structure.
        /// </summary>
        [Fact]
        public void ShallowNestingKeepsItsStructure()
        {
            // act
            var nodes = Parser.Parse("<div><p><b>x</b></p></div>");

            // validation
            var div = Assert.IsType<HtmlElementTextContentDiv>(Assert.Single(nodes));
            var p = Assert.IsType<HtmlElementTextContentP>(Assert.Single(div.Elements));
            Assert.Single(p.Elements);
        }

        /// <summary>
        /// Verifies that a stray end tag is ignored instead of ending the parse; it dropped the
        /// rest of the document.
        /// </summary>
        [Fact]
        public void StrayEndTagIsIgnored()
        {
            // act
            var nodes = Parser.Parse("<p>a</p></b><p>b</p>");

            // validation
            Assert.Equal(2, nodes.OfType<HtmlElementTextContentP>().Count());
        }

        /// <summary>
        /// Verifies that an end tag closes the elements opened inside the element it names.
        /// </summary>
        [Fact]
        public void EndTagClosesTheElementsInside()
        {
            // act
            var nodes = Parser.Parse("<div><span>a</div><p>b</p>");

            // validation
            Assert.IsType<HtmlElementTextContentDiv>(nodes[0]);
            Assert.IsType<HtmlElementTextContentP>(nodes[1]);
        }

        /// <summary>
        /// Verifies that a document full of unknown bang constructs is tokenized in linear time;
        /// every "&lt;!" searched the rest of the input for a doctype.
        /// </summary>
        [Fact]
        public void BangConstructsAreLinear()
        {
            // arrange
            var html = string.Concat(Enumerable.Repeat("<!x>", 400_000));
            var watch = Stopwatch.StartNew();

            // act
            Parser.Parse(html);

            // validation
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
        }

        /// <summary>
        /// Measures the depth of a node tree without recursion.
        /// </summary>
        /// <param name="nodes">The top-level nodes.</param>
        /// <returns>The number of levels.</returns>
        private static int Depth(IEnumerable<IHtmlNode> nodes)
        {
            var depth = 0;
            var level = nodes.ToList();

            while (level.Count > 0)
            {
                depth++;
                level = [.. level.OfType<HtmlElement>().SelectMany(x => x.Elements)];
            }

            return depth;
        }
    }
}
