using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// A node that holds ready-made HTML markup and writes it to the output verbatim, without any
    /// escaping. Only use it with trusted markup. <see cref="HtmlText"/> does not escape either, so
    /// untrusted text must be encoded (for example with <see cref="System.Net.WebUtility.HtmlEncode(string)"/>)
    /// before it is added to the page, regardless of the node type that carries it.
    /// </summary>
    public class HtmlRaw : IHtmlNode
    {
        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string Html { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlRaw()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="html">The text.</param>
        public HtmlRaw(string html)
        {
            Html = html;
        }

        /// <summary>
        /// Returns the markup unchanged.
        /// </summary>
        /// <returns>The markup as written to the output.</returns>
        public override string ToString()
        {
            return Html;
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public virtual void ToString(StringBuilder builder, int deep)
        {
            builder.Append(Html);
        }
    }
}
