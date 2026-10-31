using System.Collections.Generic;
using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// An ordered group of HTML nodes that renders its children one after another without adding any
    /// wrapping tag of its own (similar to a document fragment). Use it to return or pass around
    /// several sibling nodes as a single unit.
    /// </summary>
    public class HtmlList : IHtmlNode
    {
        private readonly List<IHtmlNode> _elements = [];

        /// <summary>
        /// Gets the elements.
        /// </summary>
        public IEnumerable<IHtmlNode> Elements => _elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlList()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlList(params IHtmlNode[] nodes)
            : this()
        {
            _elements.AddRange(nodes);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="firstNode">The first content of the html element.</param>
        /// <param name="followingNodes">The following contents of the html elements.</param>
        public HtmlList(IHtmlNode firstNode, params IHtmlNode[] followingNodes)
            : this()
        {
            _elements.Add(firstNode);
            _elements.AddRange(followingNodes);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlList(IEnumerable<IHtmlNode> nodes)
            : this()
        {
            _elements.AddRange(nodes);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="firstNode">The first content of the html element.</param>
        /// <param name="followingNodes">The following contents of the html elements.</param>
        public HtmlList(IHtmlNode firstNode, IEnumerable<IHtmlNode> followingNodes)
            : this()
        {
            _elements.Add(firstNode);
            _elements.AddRange(followingNodes);
        }

        /// <summary>
        /// Adds one or more elements to the list.
        /// </summary>
        /// <param name="elements">The elements to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        public HtmlList Add(params IHtmlNode[] elements)
        {
            _elements.AddRange(elements);

            return this;
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public void ToString(StringBuilder builder, int deep)
        {
            foreach (var v in _elements)
            {
                v.ToString(builder, deep);
            }
        }

        /// <summary>
        /// Converts the HTML list to its string representation.
        /// </summary>
        /// <returns>A string that represents the HTML list.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();

            ToString(builder, 0);

            return builder.ToString();
        }
    }
}
