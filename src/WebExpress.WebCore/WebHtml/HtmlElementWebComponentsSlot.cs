using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Renders an HTML <c>&lt;slot&gt;</c> element, a named placeholder inside a web component into which
    /// externally supplied content is projected when the component is used.
    /// </summary>
    public class HtmlElementWebFragmentsSlot : HtmlElement, IHtmlElementWebFragments
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementWebFragmentsSlot()
            : base("slot")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementWebFragmentsSlot(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
