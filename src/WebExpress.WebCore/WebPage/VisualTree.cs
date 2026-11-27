using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebPage
{
    /// <summary>
    /// The content of a page is determined by the visual tree.
    /// </summary>
    public class VisualTree : IVisualTree
    {
        /// <summary>
        /// Gets or sets the title of the html document.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets the favicons.
        /// </summary>
        public List<Favicon> Favicons { get; } = [];

        /// <summary>
        /// Gets the internal stylesheet.  
        /// </summary>
        public List<string> Styles { get; } = [];

        /// <summary>
        /// Gets the links to the java script files to be used, which are inserted in the header.
        /// </summary>
        public List<string> HeaderScriptLinks { get; } = [];

        /// <summary>
        /// Gets the links to the java script files to be used.
        /// </summary>
        public List<string> ScriptLinks { get; } = [];

        /// <summary>
        /// Gets the links to the java script files to be used, which are inserted in the header.
        /// </summary>
        public List<string> HeaderScripts { get; } = [];

        /// <summary>
        /// Gets the links to the java script files to be used.
        /// </summary>
        public IDictionary<string, string> Scripts { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets the links to the css files to be used.
        /// </summary>
        public List<string> CssLinks { get; } = [];

        /// <summary>
        /// Gets the meta information.
        /// </summary>
        public List<KeyValuePair<string, string>> Meta { get; } = [];

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public IHtmlNode Content { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public VisualTree()
        {
        }

        /// <summary>
        /// Adds or replaces a java script if it exists.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="code">The java script code.</param>
        public virtual void AddScript(string key, string code)
        {
            if (key is null) return;

            var k = key.ToLowerInvariant();
            var dict = Scripts;

            if (dict.ContainsKey(k))
            {
                dict[k] = code;
            }
            else
            {
                dict?.Add(k, code);
            }
        }

        /// <summary>
        /// Adds a java script.
        /// </summary>
        /// <param name="url">The link of the java script file.</param>
        public virtual void AddScriptLink(string url)
        {
            ScriptLinks?.Add(url);
        }

        /// <summary>
        /// Adds a java script in the header.
        /// </summary>
        /// <param name="url">The link of the java script file.</param>
        public virtual void AddHeaderScriptLinks(string url)
        {
            HeaderScriptLinks?.Add(url);
        }

        /// <summary>
        /// Converts to an HTML representation.
        /// </summary>
        /// <param name="context">The context for rendering the visual tree.</param>
        /// <returns>The page as an html tree.</returns>
        public virtual IHtmlNode Render(IVisualTreeContext context)
        {
            var html = new HtmlElementRootHtml();
            html.Head.Title = I18N.Translate(context.Request, Title);
            html.Head.Favicons = Favicons?.Select(x => new Favicon(x.Url, x.Mediatype));
            html.Head.Styles = Styles;
            html.Head.Meta = Meta;
            html.Head.Scripts = HeaderScripts;
            html.Body.Add(Content);
            html.Body.Scripts = [.. Scripts.Values];

            html.Head.CssLinks = CssLinks.Where(x => x is not null).Select(x => x.ToString());
            html.Head.ScriptLinks = HeaderScriptLinks?.Where(x => x is not null).Select(x => x.ToString());

            return html;
        }

        /// <summary>
        /// Retrieves a response based on the provided visual tree context.
        /// </summary>
        /// <param name="context">The visual tree context used to generate the response. Cannot be null.</param>
        /// <returns>A <see cref="Response"/> object representing the result of the operation.</returns>
        public Response GetResponse(IVisualTreeContext context)
        {
            return new ResponseOK()
            {
                Content = Render(context)
            };
        }
    }
}
