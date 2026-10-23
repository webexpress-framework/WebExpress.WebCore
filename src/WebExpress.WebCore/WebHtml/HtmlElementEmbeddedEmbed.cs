namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents an integration point for external resources. These are typically not
    /// html content, but for example an application or interactive content,
    /// which is represented with the help of a plugin(instead of natively by the user program).
    /// The element is a void element, so it takes no content and is written without a closing tag.
    /// </summary>
    public class HtmlElementEmbeddedEmbed : HtmlElement, IHtmlElementEmbedded
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementEmbeddedEmbed()
            : base("embed", false)
        {
        }
    }
}
