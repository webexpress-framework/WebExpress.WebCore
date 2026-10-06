namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Renders an HTML <c>&lt;col&gt;</c> element, used inside a table's column group to apply shared
    /// attributes (such as styling) to one or more table columns at once. The element is a void
    /// element, so it is written without a closing tag.
    /// </summary>
    public class HtmlElementTableCol : HtmlElement, IHtmlElementTable
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementTableCol()
            : base("col", false)
        {
        }
    }
}
