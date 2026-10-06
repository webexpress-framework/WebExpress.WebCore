using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// An HTML node that has a tag, and therefore can carry attributes (id, class, style, role, …)
    /// and contain child nodes. It exposes a fluent API (<c>Add</c>, <c>AddClass</c>, <c>AddStyle</c>, …)
    /// so elements and their content can be built up in a readable, chainable way.
    /// </summary>
    public interface IHtmlElement : IHtmlNode
    {
        /// <summary>
        /// Gets or sets the attributes.
        /// </summary>
        IEnumerable<IHtmlAttribute> Attributes { get; }

        /// <summary>
        /// Gets the elements.
        /// </summary>
        IEnumerable<IHtmlNode> Elements { get; }

        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the css class.
        /// </summary>
        public string Class { get; set; }

        /// <summary>
        /// Gets or sets the css style.
        /// </summary>
        public string Style { get; set; }

        /// <summary>
        /// Gets or sets the role.
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Gets or sets the theme.
        /// </summary>
        string DataTheme { get; set; }

        /// <summary>
        /// Adds one or more elements to the html element.
        /// </summary>
        /// <param name="elements">The elements to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement Add(params IHtmlNode[] elements);

        /// <summary>
        /// Adds one or more elements to the html element.
        /// </summary>
        /// <param name="elements">The elements to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement Add(IEnumerable<IHtmlNode> elements);

        /// <summary>
        /// Adds one or more elements to the beginning of the html element.
        /// </summary>
        /// <param name="elements">The elements to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement AddFirst(params IHtmlNode[] elements);

        /// <summary>
        /// Adds one or more attributes to the html element.
        /// </summary>
        /// <param name="attributes">The attributes to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement Add(params IHtmlAttribute[] attributes);

        /// <summary>
        /// Clear all elements from the html element.
        /// </summary>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement Clear();

        /// <summary>
        /// Adds one or more CSS class names to the current HTML element.
        /// </summary>
        /// <param name="classes">An array of CSS class names to add. Each class name must be a non-empty string.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        IHtmlElement AddClass(params string[] classes);

        /// <summary>
        /// Removes the specified CSS class or classes from the current HTML element.
        /// </summary>
        /// <param name="classes">An array of class names to remove. Each class name must be a non-empty string.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        IHtmlElement RemoveClass(params string[] classes);

        /// <summary>
        /// Adds one or more CSS class names to the current HTML element.
        /// </summary>
        /// <remarks>If a specified class name already exists on the element, it will not be added
        /// again.</remarks>
        /// <param name="styles">An array of CSS class names to add. Each class name must be a valid CSS identifier.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        IHtmlElement AddStyle(params string[] styles);

        /// <summary>
        /// Removes the specified CSS styles from the current HTML element.
        /// </summary>
        /// <remarks>If a specified style does not exist on the element, it will be ignored. This method
        /// is chainable, enabling multiple operations to be performed on the same element in a fluent manner.</remarks>
        /// <param name="styles">An array of CSS style names to remove. Each style name should correspond to a valid CSS property.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        IHtmlElement RemoveStyle(params string[] styles);

        /// <summary>
        /// Sets the valueless user-defined attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement AddUserAttribute(string name);

        /// <summary>
        /// Sets the value of an user-defined attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The value of the attribute.</param>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement AddUserAttribute(string name, string value);

        /// <summary>
        /// Removes an user-defined attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The current instance for method chaining.</returns>
        IHtmlElement RemoveUserAttribute(string name);
    }
}
