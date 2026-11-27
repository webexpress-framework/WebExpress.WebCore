using System;
using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Extension methods for html Eelements.
    /// </summary>
    public static class HtmlElementExtension
    {
        /// <summary>
        /// Adds a css class.
        /// </summary>
        /// <param name="html">The HTML element to extend.</param>
        /// <param name="cssClass">The class to add.</param>
        /// <returns>The HTML element extended by the checkout.</returns>
        public static IHtmlNode AddClass(this IHtmlNode html, string cssClass)
        {
            if (!string.IsNullOrWhiteSpace(cssClass) && html is HtmlElement element)
            {
                element.Class = Css.Concatenate([.. element.Class?.Split(" "), .. cssClass.Split(" ")]);
            }

            return html;
        }

        /// <summary>
        /// Removes a css class.
        /// </summary>
        /// <param name="html">The HTML element.</param>
        /// <param name="cssClass">The class to remove.</param>
        /// <returns>The HTML element reduced by the checkout.</returns>
        public static IHtmlNode RemoveClass(this IHtmlNode html, string cssClass)
        {
            if (html is HtmlElement element)
            {
                element.Class = Css.Remove(element.Class, cssClass);
            }

            return html;
        }

        /// <summary>
        /// Adds a style.
        /// </summary>
        /// <param name="html">The HTML element to extend.</param>
        /// <param name="cssStyle">The class to add.</param>
        /// <returns>The HTML element extended by the checkout.</returns>
        public static IHtmlNode AddStyle(this IHtmlNode html, string cssStyle)
        {
            if (!string.IsNullOrWhiteSpace(cssStyle) && html is HtmlElement element)
            {
                element.Style = Css.Concatenate([.. element.Style?.Split(" "), .. cssStyle.Split(" ")]);
            }

            return html;
        }

        /// <summary>
        /// Removes a style.
        /// </summary>
        /// <param name="html">The HTML node.</param>
        /// <param name="cssStyle">The style to remove.</param>
        /// <returns>The HTML element reduced by the checkout.</returns>
        public static IHtmlNode RemoveStyle(this IHtmlNode html, string cssStyle)
        {
            if (html is HtmlElement element)
            {
                element.Style = Css.Remove(element.Style, cssStyle);
            }

            return html;
        }

        /// <summary>
        /// Searches an HTML structure and returns all matching elements.
        /// </summary>
        /// <param name="html">The root node of the HTML structure.</param>
        /// <param name="predicate">
        /// A function that determines whether an element should be returned.
        /// </param>
        /// <returns>
        /// A collection of HTML elements that match the specified condition.
        /// </returns>
        public static IEnumerable<IHtmlNode> Find(this IHtmlNode html, Func<IHtmlNode, bool> predicate)
        {
            if (predicate(html))
            {
                yield return html;
            }

            if (html is HtmlElement element)
            {
                foreach (var child in element.Elements.OfType<IHtmlNode>())
                {
                    foreach (var descendant in child.Find(predicate))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        /// <summary>
        /// Searches an HTML element collection and returns all matching elements.
        /// </summary>
        /// <param name="nodes">The collection of HTML nodes.</param>
        /// <param name="predicate">
        /// A function that determines whether an element should be returned.
        /// </param>
        /// <returns>
        /// A collection of HTML elements that match the specified condition.
        /// </returns>
        public static IEnumerable<IHtmlNode> Find(this IEnumerable<IHtmlNode> nodes, Func<IHtmlNode, bool> predicate)
        {
            foreach (var element in nodes)
            {
                foreach (var found in element.Find(predicate))
                {
                    yield return found;
                }
            }
        }

        /// <summary>
        /// Sets the valueless user-defined attribute.
        /// </summary>
        /// <param name="html">The HTML node.</param>
        /// <param name="name">The attribute name.</param>
        /// <returns>The current instance for method chaining.</returns>
        public static IHtmlNode AddUserAttribute(this IHtmlNode html, string name)
        {
            if (html is HtmlElement element)
            {
                element.AddUserAttribute(name);
            }

            return html;
        }

        /// <summary>
        /// Sets the value of an user-defined attribute.
        /// </summary>
        /// <param name="html">The HTML node.</param>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The value of the attribute.</param>
        /// <returns>The current instance for method chaining.</returns>
        public static IHtmlNode AddUserAttribute(this IHtmlNode html, string name, string value)
        {
            if (html is HtmlElement element)
            {
                element.AddUserAttribute(name, value);
            }

            return html;
        }

        /// <summary>
        /// Removes an user-defined attribute.
        /// </summary>
        /// <param name="html">The HTML node.</param>
        /// <param name="name">The attribute name.</param>
        /// <returns>The current instance for method chaining.</returns>
        public static IHtmlNode RemoveUserAttribute(this IHtmlNode html, string name)
        {
            if (html is HtmlElement element)
            {
                element.RemoveUserAttribute(name);
            }

            return html;
        }

    }
}
