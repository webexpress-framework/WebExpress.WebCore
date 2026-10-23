using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Renders an HTML <c>&lt;blockquote&gt;</c> element, used to mark a block of text that is quoted
    /// from another source; browsers usually indent it to set it apart.
    /// </summary>
    public class HtmlElementTextContentBlockquote : HtmlElement, IHtmlElementTextContent
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementTextContentBlockquote()
            : base("blockquote")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementTextContentBlockquote(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
