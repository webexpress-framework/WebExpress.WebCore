using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WebExpress.WebCore.WebHtml.Parser;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Reduces HTML from users to markup that cannot run script: an allowlist of elements and
    /// attributes, addresses checked by <see cref="UrlPolicy"/> and inline styles reduced to
    /// harmless declarations.
    /// </summary>
    /// <remarks>
    /// HTML reaches the server from users - inline HTML in Markdown, editor content, comments - and
    /// is shown to other users. Anything that is not known to be harmless is removed, never the
    /// other way round, since a list of dangerous constructs is never complete:
    /// <list type="bullet">
    /// <item>elements that run or load content (script, style, iframe, object, form, svg, ...)
    /// are removed together with their content;</item>
    /// <item>other unknown elements are unwrapped, their content stays;</item>
    /// <item>attributes not on the list - every event handler, every data attribute the client
    /// controls act on, id - are dropped;</item>
    /// <item>comments and doctypes are dropped.</item>
    /// </list>
    /// The input is read with the same tokenizer as the parser, which separates tags and ends
    /// comments like a browser, and the output is written fresh: text and attribute values are
    /// encoded, so nothing the browser would read differently survives.
    /// </remarks>
    public static class HtmlSanitizer
    {
        /// <summary>
        /// The deepest nesting written; deeper elements are unwrapped.
        /// </summary>
        public const int MaxNestingDepth = 64;

        private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "abbr", "b", "bdi", "bdo", "blockquote", "br", "caption", "cite", "code", "col", "colgroup",
            "dd", "del", "details", "dfn", "div", "dl", "dt", "em", "figcaption", "figure", "h1", "h2", "h3",
            "h4", "h5", "h6", "hr", "i", "img", "ins", "kbd", "li", "mark", "ol", "p", "pre", "q", "s",
            "samp", "small", "span", "strong", "sub", "summary", "sup", "table", "tbody", "td", "tfoot",
            "th", "thead", "time", "tr", "u", "ul", "var", "wbr"
        };

        private static readonly HashSet<string> Removed = new(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "iframe", "frame", "frameset", "object", "embed", "applet", "template",
            "noscript", "noembed", "noframes", "xmp", "plaintext", "title", "textarea", "select", "option",
            "button", "form", "input", "svg", "math", "link", "meta", "base", "head", "audio", "video",
            "source", "track", "canvas", "portal", "dialog"
        };

        private static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase)
        {
            "br", "col", "hr", "img", "wbr"
        };

        private static readonly HashSet<string> GlobalAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "class", "title", "lang", "dir", "role", "style"
        };

        private static readonly Dictionary<string, HashSet<string>> ElementAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = new(StringComparer.OrdinalIgnoreCase) { "href" },
            ["img"] = new(StringComparer.OrdinalIgnoreCase) { "src", "alt", "width", "height" },
            ["td"] = new(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan" },
            ["th"] = new(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan", "scope" },
            ["col"] = new(StringComparer.OrdinalIgnoreCase) { "span" },
            ["colgroup"] = new(StringComparer.OrdinalIgnoreCase) { "span" },
            ["ol"] = new(StringComparer.OrdinalIgnoreCase) { "start", "type", "reversed" },
            ["li"] = new(StringComparer.OrdinalIgnoreCase) { "value" },
            ["time"] = new(StringComparer.OrdinalIgnoreCase) { "datetime" },
            ["q"] = new(StringComparer.OrdinalIgnoreCase) { "cite" },
            ["blockquote"] = new(StringComparer.OrdinalIgnoreCase) { "cite" },
            ["details"] = new(StringComparer.OrdinalIgnoreCase) { "open" }
        };

        // declarations that change how text looks but cannot move, overlay or load anything
        private static readonly HashSet<string> StyleProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "color", "background-color", "text-align", "font-weight", "font-style", "text-decoration",
            "vertical-align", "white-space"
        };

        /// <summary>
        /// Sanitizes a fragment of HTML.
        /// </summary>
        /// <param name="html">The HTML from a user.</param>
        /// <returns>The markup that remains, safe to write as it is.</returns>
        public static string Sanitize(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(html.Length);
            var open = new List<string>();
            string removing = null;
            var removingDepth = 0;

            foreach (var token in new HtmlTokenizer(html).Tokenize())
            {
                var name = token.TagName?.ToLowerInvariant();

                if (removing is not null)
                {
                    // the content of a removed element goes with it, nested elements of its kind included
                    if (token.Type == HtmlTokenType.StartTag && name == removing)
                    {
                        removingDepth++;
                    }
                    else if (token.Type == HtmlTokenType.EndTag && name == removing && --removingDepth == 0)
                    {
                        removing = null;
                    }

                    continue;
                }

                switch (token.Type)
                {
                    case HtmlTokenType.Text:
                        HtmlText.Encode(builder, WebUtility.HtmlDecode(token.Value));
                        break;

                    case HtmlTokenType.StartTag or HtmlTokenType.SelfClosingTag when name is not null && Removed.Contains(name):
                        if (token.Type == HtmlTokenType.StartTag && !Void.Contains(name))
                        {
                            removing = name;
                            removingDepth = 1;
                        }
                        break;

                    case HtmlTokenType.StartTag or HtmlTokenType.SelfClosingTag:
                        if (name is null || !Allowed.Contains(name) || open.Count >= MaxNestingDepth)
                        {
                            // unwrapped: the tag goes, its content stays
                            break;
                        }

                        WriteStartTag(builder, name, token.Attributes);

                        if (!Void.Contains(name) && token.Type == HtmlTokenType.StartTag)
                        {
                            open.Add(name);
                        }
                        break;

                    case HtmlTokenType.EndTag:
                        var index = name is null ? -1 : open.LastIndexOf(name);

                        if (index >= 0)
                        {
                            Close(builder, open, open.Count - index);
                        }
                        break;
                }
            }

            Close(builder, open, open.Count);

            return builder.ToString();
        }

        /// <summary>
        /// Writes a start tag with the attributes that are allowed for the element.
        /// </summary>
        /// <param name="builder">The output.</param>
        /// <param name="name">The element name, in lower case.</param>
        /// <param name="attributes">The attributes read.</param>
        private static void WriteStartTag(StringBuilder builder, string name, IReadOnlyList<HtmlTokenAttribute> attributes)
        {
            builder.Append('<').Append(name);

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasHref = false;

            foreach (var attribute in attributes ?? [])
            {
                var attributeName = attribute.Name?.ToLowerInvariant();

                if (attributeName is null || !written.Add(attributeName) || !IsAllowedAttribute(name, attributeName))
                {
                    continue;
                }

                var value = attribute.IsBoolean ? null : WebUtility.HtmlDecode(attribute.Value);

                value = attributeName switch
                {
                    "href" or "cite" => UrlPolicy.IsSafeLink(value) ? value : null,
                    "src" => UrlPolicy.IsSafeImage(value) ? value : null,
                    "style" => SanitizeStyle(value),
                    _ => value
                };

                if (value is null && !attribute.IsBoolean)
                {
                    continue;
                }

                hasHref |= attributeName == "href";
                builder.Append(' ').Append(attributeName);

                if (value is not null)
                {
                    builder.Append("=\"");
                    HtmlAttribute.Escape(builder, value);
                    builder.Append('"');
                }
            }

            // a link from user content opens no window that can reach back into this page
            if (name == "a" && hasHref)
            {
                builder.Append(" rel=\"nofollow noopener noreferrer\"");
            }

            builder.Append('>');
        }

        /// <summary>
        /// Tells whether an attribute is allowed on an element: a global one, one of the element's
        /// own, or an aria attribute.
        /// </summary>
        /// <param name="element">The element name.</param>
        /// <param name="attribute">The attribute name.</param>
        /// <returns>True if the attribute is kept.</returns>
        private static bool IsAllowedAttribute(string element, string attribute)
        {
            if (!HtmlName.IsValidAttributeName(attribute))
            {
                return false;
            }

            return GlobalAttributes.Contains(attribute)
                || (attribute.StartsWith("aria-", StringComparison.Ordinal) && attribute.Length > 5)
                || (ElementAttributes.TryGetValue(element, out var names) && names.Contains(attribute));
        }

        /// <summary>
        /// Keeps the declarations of an inline style whose property is on the list and whose value
        /// can neither load anything nor break out of the declaration.
        /// </summary>
        /// <param name="style">The style attribute value.</param>
        /// <returns>The remaining declarations, or null when none remain.</returns>
        private static string SanitizeStyle(string style)
        {
            if (string.IsNullOrWhiteSpace(style))
            {
                return null;
            }

            var kept = new List<string>();

            foreach (var declaration in style.Split(';'))
            {
                var colon = declaration.IndexOf(':');

                if (colon <= 0)
                {
                    continue;
                }

                var property = declaration[..colon].Trim();
                var value = declaration[(colon + 1)..].Trim();

                if (!StyleProperties.Contains(property) || value.Length == 0 || value.Length > 64
                    || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is ' ' or '#' or '%' or '.' or ',' or '-' or '(' or ')'))
                    || value.Contains("url", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("expression", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                kept.Add($"{property.ToLowerInvariant()}: {value};");
            }

            return kept.Count > 0 ? string.Join(" ", kept) : null;
        }

        /// <summary>
        /// Writes the end tags of the innermost open elements.
        /// </summary>
        /// <param name="builder">The output.</param>
        /// <param name="open">The open elements.</param>
        /// <param name="count">How many to close.</param>
        private static void Close(StringBuilder builder, List<string> open, int count)
        {
            for (var i = 0; i < count; i++)
            {
                builder.Append("</").Append(open[^1]).Append('>');
                open.RemoveAt(open.Count - 1);
            }
        }
    }
}
