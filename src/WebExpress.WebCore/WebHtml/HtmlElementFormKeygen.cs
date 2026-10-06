using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a control element for generating a pair of public and private keys and sending the public key.
    /// The element is a void element, so it takes no content and is written without a closing tag.
    /// </summary>
    public class HtmlElementFormKeygen : HtmlElement, IHtmlElementFormItem
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementFormKeygen()
            : base("keygen", false)
        {
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public override void ToString(StringBuilder builder, int deep)
        {
            base.ToString(builder, deep);
        }
    }
}
