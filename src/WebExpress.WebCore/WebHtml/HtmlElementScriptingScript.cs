using System;
using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents an internal script or a link to an external script. Java script is used as the programming language.
    /// </summary>
    public class HtmlElementScriptingScript : HtmlElement, IHtmlElementScripting
    {
        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the scripting language.
        /// </summary>
        public string Language
        {
            get => GetAttribute("language");
            set => SetAttribute("language", value);
        }

        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        public string Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        /// <summary>
        /// Gets or sets the link to the script file.
        /// </summary>
        public string Src
        {
            get => GetAttribute("src");
            set => SetAttribute("src", value);
        }

        /// <summary>
        /// Gets or sets whether the browser loads the script file while it parses the page and
        /// runs it once the page is parsed, in document order with the other deferred scripts and
        /// before DOMContentLoaded. The browser ignores it on a script written inline, which runs
        /// at once - before any deferred file; <see cref="CreateDeferred"/> defers such code.
        /// </summary>
        public bool Defer
        {
            get => HasAttribute("defer");
            set
            {
                if (value)
                {
                    SetAttribute("defer");
                }
                else
                {
                    RemoveAttribute("defer");
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the script was written by the application rather than read from
        /// markup. Only a trusted script receives the content security policy nonce of the
        /// response, so a script smuggled into parsed user content stays blocked by the browser.
        /// </summary>
        public bool Trusted { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementScriptingScript()
            : base("script")
        {
            Type = "text/javascript";
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="code">The text.</param>
        public HtmlElementScriptingScript(string code)
            : this()
        {
            Code = code;
        }

        /// <summary>
        /// Creates a script that carries its code with it and still runs deferred, in document
        /// order with the deferred script files.
        /// </summary>
        /// <remarks>
        /// The browser ignores defer on code written between the tags and runs it as soon as the
        /// parser reaches it - before the deferred files it builds on, such as the framework that
        /// defines webexpress.webui. Addressed as a data url, the code is a script file to the
        /// browser: it is deferred like the others and keeps the global scope of a classic script.
        /// The content security policy admits it by the nonce of the element.
        /// </remarks>
        /// <param name="code">The code of the script.</param>
        /// <returns>The script element.</returns>
        public static HtmlElementScriptingScript CreateDeferred(string code)
        {
            return new HtmlElementScriptingScript()
            {
                Src = "data:text/javascript;charset=utf-8;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(code ?? string.Empty)),
                Defer = true
            };
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public override void ToString(StringBuilder builder, int deep)
        {
            ToPreString(builder, deep);

            if (!string.IsNullOrWhiteSpace(Code))
            {
                // the code is emitted as written. dropping the line breaks outside a debug
                // build saved a few bytes, but a line break is syntax in JavaScript: after
                // a // comment everything up to the end of the script is swallowed, and
                // statements relying on automatic semicolon insertion run into each other.
                // Both turn a working script into a parse error that appears only in the
                // configuration that strips them. Shrinking source is a minifier's job.
                builder.Append(Code);
            }

            ToPostString(builder, deep, false);
        }

        /// <summary>
        /// Writes the opening tag and adds the nonce of the response being rendered. The nonce
        /// is appended here instead of being stored as an attribute because the same element
        /// can be serialized for several responses, each with its own nonce.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        protected override void ToPreString(StringBuilder builder, int deep)
        {
            base.ToPreString(builder, deep);

            var nonce = Trusted ? HtmlScriptNonce.Current : null;

            if (!string.IsNullOrEmpty(nonce))
            {
                // the nonce is base64 and thus never needs attribute escaping
                builder.Insert(builder.Length - 1, $" nonce=\"{nonce}\"");
            }
        }
    }
}
