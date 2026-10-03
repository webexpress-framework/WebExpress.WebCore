using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// A node without its own tag that simply writes out its (possibly empty) value. It is handy as
    /// a neutral placeholder where a node is required but no surrounding element should be produced.
    /// </summary>
    public class HtmlEmpty : IHtmlNode
    {
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlEmpty()
        {
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public virtual void ToString(StringBuilder builder, int deep)
        {
            builder.Append(Value);
        }
    }
}
