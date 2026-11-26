using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents the main content of an HTML document. Each document can contain only one body element.
    /// </summary>
    public class HtmlElementSectionBody : HtmlElement, IHtmlElementSection
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Gets or sets the code of scripts that are not files of their own. Like the
        /// <see cref="ScriptLinks"/> they are deferred, so they run after the deferred files of
        /// the head they build on.
        /// </summary>
        public List<string> Scripts { get; set; } = [];

        /// <summary>
        /// Gets or sets the links to script files. They are deferred like the ones of the head:
        /// loaded synchronously at the end of the body, they would run before those.
        /// </summary>
        public List<string> ScriptLinks
        {
            get => [.. ElementScriptLinks.Select(x => x.Src)];
            set
            {
                ElementScriptLinks.Clear();
                ElementScriptLinks.AddRange(value.Select(x => new HtmlElementScriptingScript()
                {
                    Language = "javascript",
                    Src = x,
                    Type = "text/javascript",
                    Defer = true
                }));
            }
        }

        /// <summary>
        /// Gets or sets the external scripts.
        /// </summary>
        private List<HtmlElementScriptingScript> ElementScriptLinks { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementSectionBody()
            : base("body")
        {
            ElementScriptLinks = [];
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementSectionBody(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public override void ToString(StringBuilder builder, int deep)
        {
            ToPreString(builder, deep);

            foreach (var v in Elements.Where(x => x is not null))
            {
                v.ToString(builder, deep + 1);
            }

            foreach (var v in ElementScriptLinks)
            {
                v.ToString(builder, deep + 1);
            }

            foreach (var script in Scripts)
            {
                HtmlElementScriptingScript.CreateDeferred(script).ToString(builder, deep + 1);
            }

            ToPostString(builder, deep, true);
        }
    }
}
