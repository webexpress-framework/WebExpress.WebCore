using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a collection of metadata of the document. This includes links to or definitions 
    /// of scripts and style sheets.
    /// </summary>
    public class HtmlElementMetadataHead : HtmlElement, IHtmlElementMetadata
    {
        private readonly HtmlElementMetadataTitle _elementTitle = new HtmlElementMetadataTitle();
        private readonly HtmlElementMetadataBase _elementBase = new HtmlElementMetadataBase();
        private IEnumerable<HtmlElementMetadataLink> _elementFavicons = [];
        private IEnumerable<HtmlElementMetadataStyle> _elementStyles = [];
        private IReadOnlyList<string> _scripts = [];
        private IEnumerable<HtmlElementScriptingScript> _elementScripts = [];
        private IEnumerable<HtmlElementScriptingScript> _elementScriptLinks = [];
        private IEnumerable<HtmlElementMetadataLink> _elementCssLinks = [];
        private IEnumerable<HtmlElementMetadataMeta> _elementMeta = [];

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title
        {
            get => _elementTitle.Title;
            set => _elementTitle.Title = value;
        }

        /// <summary>
        /// Gets or sets the base.
        /// </summary>
        public string Base
        {
            get => _elementBase.Href;
            set => _elementBase.Href = value;
        }

        /// <summary>
        /// Gets or sets the favicon.
        /// </summary>
        public IEnumerable<Favicon> Favicons
        {
            get => _elementFavicons.Select(x => new Favicon(x.Href, x.Type));
            set
            {
                _elementFavicons = value.Select(x => new HtmlElementMetadataLink()
                {
                    Href = x.Url,
                    Rel = "icon",
                    Type = x.Mediatype != TypeFavicon.Default ? x.GetMediaType() : ""
                });
            }
        }

        /// <summary>
        /// Gets or sets the internal stylesheet.
        /// </summary>
        public IEnumerable<string> Styles
        {
            get => _elementStyles.Select(x => x.Code);
            set { _elementStyles = value.Select(x => new HtmlElementMetadataStyle(x)); }
        }

        /// <summary>
        /// Gets or sets the code of scripts that are not files of their own. They run after the
        /// <see cref="ScriptLinks"/> and in the order given, as they did while the files were
        /// loaded synchronously: a script such as a set of translations calls the framework the
        /// files define, which written inline would run before any deferred file.
        /// </summary>
        public IEnumerable<string> Scripts
        {
            get => _scripts;
            set
            {
                _scripts = [.. value ?? []];
                _elementScripts = [.. _scripts.Select(HtmlElementScriptingScript.CreateDeferred)];
            }
        }

        /// <summary>
        /// Gets or sets the links to script files.
        /// </summary>
        /// <remarks>
        /// Every file is deferred: a script in the head stops the parser until it is loaded and
        /// run, and a page links several hundred, so the page showed nothing before all of them
        /// had arrived. Deferred, they load while the page is parsed and still run in the order
        /// they are linked, before DOMContentLoaded, where the controls are built.
        /// </remarks>
        public IEnumerable<string> ScriptLinks
        {
            get => _elementScriptLinks.Select(x => x.Src);
            set
            {
                _elementScriptLinks = value.Select(x => new HtmlElementScriptingScript()
                {
                    Language = "javascript",
                    Src = x,
                    Type = "text/javascript",
                    Defer = true
                });
            }
        }

        /// <summary>
        /// Gets or sets the internal stylesheet.
        /// </summary>
        public IEnumerable<string> CssLinks
        {
            get => _elementCssLinks.Select(x => x.Href);
            set
            {
                _elementCssLinks = value.Select(x => new HtmlElementMetadataLink()
                {
                    Rel = "stylesheet",
                    Href = x,
                    Type = "text/css"
                });
            }
        }

        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Meta
        {
            get => _elementMeta.Select(x => new KeyValuePair<string, string>(x.Key, x.Value));
            set { _elementMeta = value.Select(x => new HtmlElementMetadataMeta(x.Key, x.Value)); }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementMetadataHead()
            : base("head")
        {
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public override void ToString(StringBuilder builder, int deep)
        {
            ToPreString(builder, deep);

            if (!string.IsNullOrWhiteSpace(Title))
            {
                _elementTitle.ToString(builder, deep + 1);
            }

            if (!string.IsNullOrWhiteSpace(Base))
            {
                _elementBase.ToString(builder, deep + 1);
            }

            foreach (var v in _elementFavicons)
            {
                v.ToString(builder, deep + 1);
            }

            foreach (var v in _elementStyles)
            {
                v.ToString(builder, deep + 1);
            }

            foreach (var v in _elementScriptLinks)
            {
                v.ToString(builder, deep + 1);
            }

            foreach (var v in _elementScripts)
            {
                v.ToString(builder, deep + 1);
            }

            foreach (var v in _elementCssLinks)
            {
                v.ToString(builder, deep + 1);
            }

            foreach (var v in _elementMeta)
            {
                v.ToString(builder, deep + 1);
            }

            ToPostString(builder, deep, true);
        }
    }
}
