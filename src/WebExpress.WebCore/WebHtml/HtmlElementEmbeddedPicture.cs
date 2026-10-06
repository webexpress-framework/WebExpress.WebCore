using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Renders an HTML <c>&lt;picture&gt;</c> element, a container that lets the browser choose between
    /// several image sources (for example by screen size or format) and falls back to a contained image.
    /// </summary>
    public class HtmlElementEmbeddedPicture : HtmlElement, IHtmlElementEmbedded
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementEmbeddedPicture()
            : base("picture")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementEmbeddedPicture(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
