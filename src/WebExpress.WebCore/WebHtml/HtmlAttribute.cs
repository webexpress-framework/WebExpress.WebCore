using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents an HTML attribute that can be added to an HTML element.
    /// </summary>
    public class HtmlAttribute : IHtmlAttribute
    {
        /// <summary>
        /// Gets or sets the name of the attribute.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlAttribute()
        {

        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="name">The name.</param>
        public HtmlAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public HtmlAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Convert to a string using a string builder.
        /// </summary>
        /// <remarks>
        /// The value is escaped here rather than by whoever supplied it. A value carrying a
        /// double quote would otherwise end the attribute early and the rest of it would land in
        /// the markup as stray attributes - which is not a theoretical risk, because json in a
        /// data attribute is how a control hands structured state to its client counterpart.
        /// Callers therefore pass their value as it is; encoding it before handing it over now
        /// produces <c>&amp;amp;quot;</c>.
        /// </remarks>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public virtual void ToString(StringBuilder builder, int deep)
        {
            builder.Append(Name);
            builder.Append("=\"");
            Escape(builder, Value);
            builder.Append('"');
        }

        /// <summary>
        /// Appends a value, escaping what a double-quoted attribute cannot carry.
        /// </summary>
        /// <remarks>
        /// Only the ampersand and the double quote are escaped, and deliberately not more. The
        /// quote is what would end the attribute; the ampersand is what would otherwise turn the
        /// text following it into an entity - <c>?a=1&amp;copy=2</c> reads as a copyright sign.
        /// <c>&lt;</c>, <c>&gt;</c> and the apostrophe are legal inside a double-quoted value,
        /// and a non-ascii character is simply itself on a utf-8 document; escaping either would
        /// only make the markup harder to read for no gain.
        /// </remarks>
        /// <param name="builder">The string builder.</param>
        /// <param name="value">The value to append, may be null.</param>
        private static void Escape(StringBuilder builder, string value)
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
                    case '"': builder.Append("&quot;"); break;
                    default: builder.Append(c); break;
                }
            }
        }
    }
}
