using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a alternate content to display when the browser does not support scripting.
    /// </summary>
    public class HtmlElementScriptingNoscript : HtmlElement, IHtmlElementScripting
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementScriptingNoscript()
            : base("noscript")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementScriptingNoscript(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
