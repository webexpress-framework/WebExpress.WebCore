using System;
using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Used to define metadata that cannot be defined with any other HTML element.
    /// </summary>
    public class HtmlElementMetadataMeta : HtmlElement, IHtmlElementMetadata
    {
        /// <summary>
        /// The keys that name a meta declaration in their own right. Everything
        /// else is a named declaration and belongs in a name/content pair.
        /// </summary>
        private static readonly string[] _standaloneKeys = ["charset"];

        /// <summary>
        /// Gets or sets the attribute name.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value
        {
            get => GetAttribute(Key);
            set => SetAttribute(Key, value);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementMetadataMeta()
            : base("meta")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementMetadataMeta(string key)
            : this()
        {
            Key = key;
            SetAttribute(Key, "");
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementMetadataMeta(string key, string value)
            : this()
        {
            Key = key;
            SetAttribute(Key, value);
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <remarks>
        /// Only charset carries its value directly; every other declaration is
        /// addressed by name and holds its value in content. Writing them all as
        /// a single attribute produced markup a browser has no rule for and
        /// silently dropped - which is how the viewport declaration came to have
        /// no effect on small screens.
        /// </remarks>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public override void ToString(StringBuilder builder, int deep)
        {
            builder.AppendLine();
            builder.Append(string.Empty.PadRight(deep));
            builder.Append("<");
            builder.Append(ElementName);
            builder.Append(" ");

            if (Array.Exists(_standaloneKeys, x => x.Equals(Key, StringComparison.OrdinalIgnoreCase)))
            {
                builder.Append(Key);
                builder.Append("='");
                builder.Append(Value);
                builder.Append("'>");

                return;
            }

            builder.Append("name='");
            builder.Append(Key);
            builder.Append("' content='");
            builder.Append(Value);
            builder.Append("'>");
        }
    }
}
