using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Renders an HTML <c>&lt;menu&gt;</c> element, a list of commands or options presented to the user
    /// (semantically a toolbar- or menu-style grouping of interactive items).
    /// </summary>
    public class HtmlElementInteractiveMenu : HtmlElement, IHtmlElementInteractive
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementInteractiveMenu()
            : base("menu")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementInteractiveMenu(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
