using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a user input, often, but not necessarily, on the keyboard. Can also stand 
    /// for other inputs, such as transcribed voice commands.
    /// </summary>
    public class HtmlElementTextSemanticsKdb : HtmlElement, IHtmlElementTextSemantics
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementTextSemanticsKdb()
            : base("kbd")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementTextSemanticsKdb(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
