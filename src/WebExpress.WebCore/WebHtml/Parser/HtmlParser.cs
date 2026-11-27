using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace WebExpress.WebCore.WebHtml.Parser
{
    /// <summary>
    /// Parses an HTML string and reconstructs the corresponding
    /// <see cref="IHtmlNode"/> object tree using the classes from
    /// <c>WebExpress.WebCore.WebHtml</c>.
    /// </summary>
    /// <remarks>
    /// The parser consumes the token stream produced by <see cref="HtmlTokenizer"/>
    /// and builds a DOM-like hierarchy of <see cref="IHtmlNode"/> objects.  It is
    /// deliberately tolerant: unknown tags are mapped to a generic
    /// <see cref="HtmlElement"/>, and unclosed tags are automatically closed when
    /// an end token for an ancestor is encountered (or when the token stream ends).
    /// </remarks>
    public class HtmlParser
    {
        /// <summary>
        /// The deepest nesting of elements the parser builds; deeper elements are kept without
        /// content and their content becomes their siblings.
        /// </summary>
        public const int MaxNestingDepth = 256;

        // ------------------------------------------------------------------
        // Public entry points
        // ------------------------------------------------------------------

        /// <summary>
        /// Parses the supplied HTML string and returns the top-level nodes.
        /// </summary>
        /// <param name="html">The HTML string to parse.</param>
        /// <returns>
        /// A read-only list of <see cref="IHtmlNode"/> objects that form the
        /// top-level content of the parsed document.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is <c>null</c>.</exception>
        /// <exception cref="HtmlParseException">Thrown when the HTML is so malformed that recovery is impossible.</exception>
        public IReadOnlyList<IHtmlNode> Parse(string html)
        {
            if (html is null)
            {
                throw new ArgumentNullException(nameof(html));
            }

            var tokenizer = new HtmlTokenizer(html);
            var tokens = tokenizer.Tokenize();
            return ParseTokens(tokens);
        }

        /// <summary>
        /// Parses the supplied HTML string and returns the single root node.
        /// </summary>
        /// <param name="html">The HTML string to parse.</param>
        /// <returns>
        /// The first top-level <see cref="IHtmlNode"/>, or <c>null</c> if the
        /// input produces no nodes.
        /// </returns>
        public IHtmlNode ParseSingle(string html)
        {
            var nodes = Parse(html);
            return nodes.Count > 0 ? nodes[0] : null;
        }

        // ------------------------------------------------------------------
        // Core parsing logic
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the node tree from the token stream with an explicit stack of open elements.
        /// </summary>
        /// <remarks>
        /// The tree used to be built by a recursive call per start tag. HTML reaches the parser
        /// from users - pasted into the editor, stored as a comment, converted to Markdown - and a
        /// few hundred kilobytes of nested tags exhausted the stack; a stack overflow cannot be
        /// caught in .NET and ended the server process. An element opened deeper than
        /// <see cref="MaxNestingDepth"/> is kept as an empty element, its content becoming its
        /// siblings, so every renderer that recurses through the tree stays bounded as well.
        ///
        /// An end tag closes the innermost open element of its name together with everything
        /// opened inside it, as a browser does; an end tag without an open element of its name is
        /// ignored. A stray end tag used to stop the parse and drop the rest of the document.
        /// </remarks>
        /// <param name="tokens">The token stream.</param>
        /// <returns>The top-level nodes.</returns>
        private static List<IHtmlNode> ParseTokens(IReadOnlyList<HtmlToken> tokens)
        {
            var roots = new List<IHtmlNode>();
            var open = new List<(HtmlElement Element, string TagName, List<IHtmlNode> Children)>();

            List<IHtmlNode> Target() => open.Count > 0 ? open[^1].Children : roots;

            void Close(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    var (element, _, children) = open[^1];
                    open.RemoveAt(open.Count - 1);
                    element.Add([.. children]);
                }
            }

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case HtmlTokenType.EndOfFile:
                        Close(open.Count);
                        return roots;

                    case HtmlTokenType.Doctype:
                        // DOCTYPE tokens are informational; no node is emitted.
                        break;

                    case HtmlTokenType.Comment:
                        Target().Add(new HtmlComment(token.Value));
                        break;

                    case HtmlTokenType.Text:
                        if (!string.IsNullOrEmpty(token.Value))
                        {
                            // the content of script, style and the other raw text elements is
                            // no html and must neither be decoded nor encoded; everything else is
                            // stored decoded, since HtmlText encodes what it writes
                            Target().Add(open.Count > 0 && IsRawText(open[^1].TagName)
                                ? new HtmlRaw(token.Value)
                                : new HtmlText(WebUtility.HtmlDecode(token.Value)));
                        }
                        break;

                    case HtmlTokenType.SelfClosingTag or HtmlTokenType.StartTag or HtmlTokenType.EndTag
                        when !HtmlName.IsValidElementName(token.TagName):
                        // a name that could not be written back safely is dropped; its content
                        // stays with the enclosing element
                        break;

                    case HtmlTokenType.SelfClosingTag:
                        var selfClosing = HtmlElementFactory.Create(token.TagName);
                        ApplyAttributes(selfClosing, token.Attributes);
                        Target().Add(selfClosing);
                        break;

                    case HtmlTokenType.StartTag:
                        var element = HtmlElementFactory.Create(token.TagName);
                        ApplyAttributes(element, token.Attributes);
                        Target().Add(element);

                        if (element.CloseTag && open.Count < MaxNestingDepth)
                        {
                            open.Add((element, token.TagName, []));
                        }
                        break;

                    case HtmlTokenType.EndTag:
                        var index = open.FindLastIndex(x => string.Equals(x.TagName, token.TagName, StringComparison.OrdinalIgnoreCase));

                        if (index >= 0)
                        {
                            Close(open.Count - index);
                        }
                        break;
                }
            }

            Close(open.Count);

            return roots;
        }

        /// <summary>
        /// Tells whether an element holds raw text, whose content the browser reads literally.
        /// </summary>
        /// <param name="tagName">The name of the element.</param>
        /// <returns>True for script, style, xmp, iframe, noembed, noframes and plaintext.</returns>
        private static bool IsRawText(string tagName)
        {
            return tagName?.ToLowerInvariant() is "script" or "style" or "xmp" or "iframe" or "noembed" or "noframes" or "plaintext";
        }

        // ------------------------------------------------------------------
        // Attribute mapping
        // ------------------------------------------------------------------

        private static void ApplyAttributes(HtmlElement element, IReadOnlyList<HtmlTokenAttribute> attributes)
        {
            if (attributes is null || attributes.Count == 0)
            {
                return;
            }

            foreach (var attr in attributes)
            {
                if (!HtmlName.IsValidAttributeName(attr.Name))
                {
                    continue;
                }

                if (attr.IsBoolean)
                {
                    element.AddUserAttribute(attr.Name);
                }
                else
                {
                    element.AddUserAttribute(attr.Name, attr.Value);
                }
            }
        }
    }
}
