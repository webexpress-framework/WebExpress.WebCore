using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a heading grouped with associated content, such as subheadings or taglines.
    /// </summary>
    public class HtmlElementSectionHgroup : HtmlElement, IHtmlElementSection
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementSectionHgroup()
            : base("hgroup")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementSectionHgroup(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
