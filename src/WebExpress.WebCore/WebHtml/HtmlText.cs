using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// A node that holds a piece of plain text placed between or inside HTML elements (a text node).
    /// Use it to add textual content to the page; for ready-made markup use <see cref="HtmlRaw"/> instead.
    /// </summary>
    /// <remarks>
    /// The value is plain text and is HTML-encoded when it is written: <c>&lt;</c>, <c>&gt;</c> and
    /// <c>&amp;</c> become entity references, so text from users can never become markup. The
    /// value used to be written verbatim and left the encoding to every caller - over a hundred
    /// places that each had to remember it, and any that forgot turned user input into script.
    /// The parser stores the text it reads decoded, so a parsed document written out again keeps
    /// its entities. A caller holding markup it trusts writes it with <see cref="HtmlRaw"/>.
    /// </remarks>
    public class HtmlText : IHtmlNode
    {
        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlText()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The text.</param>
        public HtmlText(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public virtual void ToString(StringBuilder builder, int deep)
        {
            Encode(builder, Value);
        }

        /// <summary>
        /// Appends text HTML-encoded for the content of an element.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="value">The text.</param>
        public static void Encode(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var c in value)
            {
                switch (c)
                {
                    case '&': builder.Append("&amp;"); break;
                    case '<': builder.Append("&lt;"); break;
                    case '>': builder.Append("&gt;"); break;
                    default: builder.Append(c); break;
                }
            }
        }
    }
}
