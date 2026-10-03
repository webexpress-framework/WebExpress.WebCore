namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents any node that can appear in the HTML output tree — an element, a piece of text,
    /// a comment, or raw markup. It is the common type for everything that can be nested inside an
    /// HTML element, so containers can hold mixed content without caring about the concrete kind.
    /// </summary>
    public interface IHtmlNode : IHtml
    {
    }
}
