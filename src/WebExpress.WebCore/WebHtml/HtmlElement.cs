using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// The basis of all html elements (see RfC 1866).
    /// </summary>
    public class HtmlElement : IHtmlElement
    {
        private readonly List<IHtmlNode> _elements = [];
        private readonly List<IHtmlAttribute> _attributes = [];

        /// <summary>
        /// Gets or sets the name of the element.
        /// </summary>
        protected string ElementName { get; set; }

        /// <summary>
        /// Gets the attributes.
        /// </summary>
        public IEnumerable<IHtmlAttribute> Attributes => _attributes;

        /// <summary>
        /// Gets the elements.
        /// </summary>
        public IEnumerable<IHtmlNode> Elements => _elements;

        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id
        {
            get => GetAttribute("id");
            set => SetAttribute("id", value);
        }

        /// <summary>
        /// Gets or sets the css class.
        /// </summary>
        public string Class
        {
            get => GetAttribute("class");
            set => SetAttribute("class", value);
        }

        /// <summary>
        /// Gets or sets the css style.
        /// </summary>
        public string Style
        {
            get => GetAttribute("style");
            set => SetAttribute("style", value);
        }

        /// <summary>
        /// Gets or sets the role.
        /// </summary>
        public string Role
        {
            get => GetAttribute("role");
            set => SetAttribute("role", value);
        }

        /// <summary>
        /// Gets or sets the html5 data attribute.
        /// </summary>
        public string DataToggle
        {
            get => GetAttribute("data-toggle");
            set => SetAttribute("data-toggle", value);
        }

        /// <summary>
        /// Gets or sets the html5 data attribute.
        /// </summary>
        public string DataProvide
        {
            get => GetAttribute("data-provide");
            set => SetAttribute("data-provide", value);
        }

        /// <summary>
        /// Gets or sets the theme.
        /// </summary>
        public string DataTheme
        {
            get => GetAttribute("data-bs-theme");
            set => SetAttribute("data-bs-theme", value);
        }

        /// <summary>
        /// Gets or sets the on click attribute.
        /// </summary>
        public string OnClick
        {
            get => GetAttribute("onclick");
            set => SetAttribute("onclick", value);
        }

        /// <summary>
        /// Determines whether the element is written without the line break and indentation
        /// that otherwise precede it. Independent of this, no break is written where it would
        /// touch text, because a browser reads it as a blank there (see <see cref="ContinuesText"/>).
        /// </summary>
        public bool Inline { get; set; }

        /// <summary>
        /// The builder and its length right after the opening tag of a text-level element was
        /// written, so the first child of that element can tell that it continues running
        /// text. Serialization runs on one thread from start to end, which keeps this
        /// thread-local state consistent.
        /// </summary>
        [ThreadStatic]
        private static StringBuilder _textLevelOpenBuilder;

        [ThreadStatic]
        private static int _textLevelOpenEnd;

        /// <summary>
        /// Determines whether the element requires a closing tag.
        /// Examples: true → &lt;div&gt;&lt;/div&gt;, false → &lt;br/&gt;
        /// This affects rendering behavior in ToString and ToPostString.
        /// </summary>
        public bool CloseTag { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="name">The name of the HTML element.</param>
        /// <param name="closeTag">A boolean value indicating whether the element requires a self closing tag. Default is true.</param>
        public HtmlElement(string name, bool closeTag = true)
        {
            ElementName = name;
            CloseTag = closeTag;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="name">The name of the HTML element.</param>
        /// <param name="closeTag">A boolean value indicating whether the element requires a closing tag.</param>
        /// <param name="nodes">An array of IHtml nodes to be added to the element.</param>
        public HtmlElement(string name, bool closeTag, params IHtml[] nodes)
            : this(name, closeTag)
        {
            foreach (var v in nodes)
            {
                if (v is HtmlAttribute attr)
                {
                    _attributes.Add(attr);
                }
                else if (v is HtmlElement element)
                {
                    _elements.Add(element);
                }
                else if (v is HtmlText text)
                {
                    _elements.Add(text);
                }
            }
        }

        /// <summary>
        /// Adds one or more elements to the html element.
        /// </summary>
        /// <param name="elements">The elements to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement Add(params IHtmlNode[] elements)
        {
            _elements.AddRange(elements);

            return this;
        }

        /// <summary>
        /// Adds one or more elements to the html element.
        /// </summary>
        /// <param name="elements">The elements to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement Add(IEnumerable<IHtmlNode> elements)
        {
            _elements.AddRange(elements);

            return this;
        }

        /// <summary>
        /// Adds one or more elements to the beginning of the html element.
        /// </summary>
        /// <param name="elements">The elements to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement AddFirst(params IHtmlNode[] elements)
        {
            _elements.InsertRange(0, elements);

            return this;
        }

        /// <summary>
        /// Adds one or more attributes to the html element.
        /// </summary>
        /// <param name="attributes">The attributes to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement Add(params IHtmlAttribute[] attributes)
        {
            _attributes.AddRange(attributes);

            return this;
        }

        /// <summary>
        /// Adds one or more CSS class names to the current HTML element.
        /// </summary>
        /// <param name="classes">An array of CSS class names to add. Each class name must be a non-empty string.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public IHtmlElement AddClass(params string[] classes)
        {
            Class = Css.Concatenate(Class, classes);

            return this;
        }

        /// <summary>
        /// Removes the specified CSS class or classes from the current HTML element.
        /// </summary>
        /// <param name="classes">An array of class names to remove. Each class name must be a non-empty string.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public IHtmlElement RemoveClass(params string[] classes)
        {
            Class = Css.Remove(Class, classes);

            return this;
        }

        /// <summary>
        /// Adds one or more CSS class names to the current HTML element.
        /// </summary>
        /// <remarks>If a specified class name already exists on the element, it will not be added
        /// again.</remarks>
        /// <param name="styles">An array of CSS class names to add. Each class name must be a valid CSS identifier.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public IHtmlElement AddStyle(params string[] styles)
        {
            Style = Css.Concatenate(Style, styles);

            return this;
        }

        /// <summary>
        /// Removes the specified CSS styles from the current HTML element.
        /// </summary>
        /// <remarks>If a specified style does not exist on the element, it will be ignored. This method
        /// is chainable, enabling multiple operations to be performed on the same element in a fluent manner.</remarks>
        /// <param name="styles">An array of CSS style names to remove. Each style name should correspond to a valid CSS property.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public IHtmlElement RemoveStyle(params string[] styles)
        {
            Style = Css.Remove(Style, styles);

            return this;
        }

        /// <summary>
        /// Clear all elements from the html element.
        /// </summary>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement Clear()
        {
            _elements.Clear();

            return this;
        }

        /// <summary>
        /// Clear all elements from the html element that match the given predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match elements.</param>
        protected void Clear(Func<IHtmlNode, bool> predicate)
        {
            _elements.RemoveAll(new Predicate<IHtmlNode>(predicate));
        }

        /// <summary>
        /// Returns the value of an attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The value of the attribute.</returns>
        protected string GetAttribute(string name)
        {
            var a = _attributes.FirstOrDefault(x => x.Name == name);

            if (a is not null)
            {
                return a is HtmlAttribute attribute ? attribute.Value : string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Checks whether an attribute is set.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>True if attribute exists, false otherwise.</returns>
        protected bool HasAttribute(string name)
        {
            var a = _attributes.FirstOrDefault(x => x.Name == name);

            return (a is not null);
        }

        /// <summary>
        /// Sets the value of an attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The value of the attribute.</param>
        protected void SetAttribute(string name, string value)
        {
            var a = _attributes.FirstOrDefault(x => x.Name == name);

            if (a is not null)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _attributes.Remove(a);
                }
                else if (a is HtmlAttribute attribute)
                {
                    attribute.Value = value;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _attributes.Add(new HtmlAttribute(name, value));
                }
            }
        }

        /// <summary>
        /// Sets an attribute without a value
        /// </summary>
        /// <param name="name">The attribute name.</param>
        protected void SetAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var a = _attributes.FirstOrDefault(x => x.Name == name);

            if (a is null)
            {
                _attributes.Add(new HtmlAttributeNoneValue(name));
            }
        }

        /// <summary>
        /// Removes an attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        protected void RemoveAttribute(string name)
        {
            var a = _attributes.FirstOrDefault(x => x.Name == name);

            if (a is not null)
            {
                _attributes.Remove(a);
            }
        }

        /// <summary>
        /// Returns an element based on its name.
        /// </summary>
        /// <param name="name">The element name.</param>
        /// <returns>The element.</returns>
        protected HtmlElement GetElement(string name)
        {
            var a = _elements.FirstOrDefault(x => x is HtmlElement element && element.ElementName == name);

            return a as HtmlElement;
        }

        /// <summary>
        /// Sets an element.
        /// </summary>
        /// <param name="element">The element.</param>
        protected void SetElement(HtmlElement element)
        {
            if (element is not null)
            {
                var a = _elements.Where(x => x is HtmlElement && (x as HtmlElement).ElementName == element.ElementName);

                foreach (var v in a)
                {
                    _elements.Remove(v);
                }

                _elements.Add(element);
            }
        }

        /// <summary>
        /// Returns the text.
        /// </summary>
        /// <returns>The text.</returns>
        protected string GetText()
        {
            var a = _elements.Where(x => x is HtmlText).Select(x => (x as HtmlText).Value);

            return string.Join(" ", a);
        }

        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        public virtual void ToString(StringBuilder builder, int deep)
        {
            var closeTag = false;
            var nl = true;

            ToPreString(builder, deep);

            if (_elements.Count == 0)
            {
                nl = false;
                closeTag = CloseTag;
            }
            else if (ContainsOnlyTextNodes(_elements, out var text))
            {
                nl = false;
                closeTag = true;
                builder.Append(text);
            }
            else
            {
                closeTag = true;
                var count = builder.Length;

                foreach (var v in _elements.Where(x => x is not null))
                {
                    v.ToString(builder, deep + 1);
                }

                if (count == builder.Length)
                {
                    nl = false;
                }
            }

            if (closeTag)
            {
                ToPostString(builder, deep, nl);
            }

            // the mark only concerns the first child, and a page builder must not outlive
            // its request in a thread-local field
            if (ReferenceEquals(_textLevelOpenBuilder, builder))
            {
                _textLevelOpenBuilder = null;
            }
        }

        /// <summary>
        /// Converts the element to a string and appends it to the provided StringBuilder.
        /// </summary>
        /// <param name="builder">The StringBuilder to append the string representation to.</param>
        /// <param name="deep">The depth of the element in the HTML hierarchy, used for indentation.</param>
        protected virtual void ToPreString(StringBuilder builder, int deep)
        {
            if (!Inline && !ContinuesText(builder))
            {
                builder.AppendLine();
                builder.Append(string.Empty.PadRight(deep));
            }

            builder.Append('<');
            builder.Append(ElementName);
            foreach (var attribute in Attributes)
            {
                builder.Append(' ');
                attribute.ToString(builder, 0);
            }

            builder.Append('>');

            if (this is IHtmlElementTextSemantics)
            {
                _textLevelOpenBuilder = builder;
                _textLevelOpenEnd = builder.Length;
            }
        }

        /// <summary>
        /// Determines whether an element written next continues running text: it follows
        /// text directly, or it is the first child of a text-level element. A line break
        /// there would be read as a blank and split a word formatted in part
        /// (<c>x&lt;b&gt;y&lt;/b&gt;z</c>). Between two elements the break is kept, because
        /// controls place an icon and its label as neighbours and rely on the blank between them.
        /// </summary>
        /// <param name="builder">The builder the element is written to.</param>
        /// <returns>True if the element continues running text; otherwise, false.</returns>
        private static bool ContinuesText(StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                return false;
            }

            if (ReferenceEquals(builder, _textLevelOpenBuilder) && builder.Length == _textLevelOpenEnd)
            {
                return true;
            }

            var last = builder[builder.Length - 1];

            return last != '>' && !char.IsWhiteSpace(last);
        }

        /// <summary>
        /// Converts the element to a string and appends the closing tag to the provided StringBuilder.
        /// </summary>
        /// <param name="builder">The StringBuilder to append the string representation to.</param>
        /// <param name="deep">The depth of the element in the HTML hierarchy, used for indentation.</param>
        /// <param name="nl">Indicates whether the closing tag should start on a new line.</param>
        protected virtual void ToPostString(StringBuilder builder, int deep, bool nl = true)
        {
            // a break before the closing tag of a text-level element would be a blank inside
            // the running text, visible before whatever follows it, such as a full stop
            if (!Inline && nl && this is not IHtmlElementTextSemantics)
            {
                builder.AppendLine();
                builder.Append(string.Empty.PadRight(deep));
            }

            builder.Append("</");
            builder.Append(ElementName);
            builder.Append('>');
        }

        /// <summary>
        /// Determines whether the collection of IHtmlNode instances (including nested HtmlElement children)
        /// contains only HtmlText nodes. If so, combines their text content and returns it via an out parameter.
        /// </summary>
        /// <param name="elements">A collection of IHtmlNode instances to inspect.</param>
        /// <param name="combinedText">The combined text content if all nodes are HtmlText; otherwise, null.</param>
        /// <returns>
        /// True if all nodes (and their descendants) are HtmlText; otherwise, false.
        /// </returns>
        public bool ContainsOnlyTextNodes(IEnumerable<IHtmlNode> elements, out string combinedText)
        {
            var builder = new StringBuilder();

            foreach (var node in elements)
            {
                switch (node)
                {
                    case HtmlText text:
                        builder.Append(text.Value);
                        break;
                    case HtmlList list:
                        if (!ContainsOnlyTextNodes(list.Elements, out var nestedListText))
                        {
                            combinedText = null;
                            return false;
                        }
                        builder.Append(nestedListText);
                        break;
                    case HtmlElement element:
                        combinedText = null;
                        return false;
                    default:
                        combinedText = null;
                        return false;
                }
            }

            combinedText = builder.ToString();
            return true;
        }


        /// <summary>
        /// Sets the valueless user-defined attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement AddUserAttribute(string name)
        {
            SetAttribute(name);

            return this;
        }

        /// <summary>
        /// Sets the value of an user-defined attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The value of the attribute.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement AddUserAttribute(string name, string value)
        {
            SetAttribute(name, value);

            return this;
        }

        /// <summary>
        /// Returns the value of an user-defined attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The value of the attribute.</returns>
        public string GetUserAttribute(string name)
        {
            return GetAttribute(name);
        }

        /// <summary>
        /// Checks if a user-defined attribute is set.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>True wenn Attribut vorhanden, false sonst</returns>
        public bool HasUserAttribute(string name)
        {
            return HasAttribute(name);
        }

        /// <summary>
        /// Removes an user-defined attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IHtmlElement RemoveUserAttribute(string name)
        {
            RemoveAttribute(name);

            return this;
        }

        /// <summary>
        /// Convert to String.
        /// </summary>
        /// <returns>The object as a string.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();
            ToString(builder, 0);

            return builder.ToString();
        }
    }
}
