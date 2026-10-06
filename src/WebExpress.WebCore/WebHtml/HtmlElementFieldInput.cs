using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a field for user input of a specific type. The type (radio button, checkbox, 
    /// text input, etc.) is specified using the type attribute.
    /// </summary>
    public class HtmlElementFieldInput : HtmlElement, IHtmlElementFormItem
    {
        /// <summary>
        /// Gets or sets the name of the input field.
        /// </summary>
        public string Name
        {
            get => GetAttribute("name");
            set => SetAttribute("name", value);
        }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        public string Type
        {
            get => GetAttribute("type");
            set => SetAttribute("type", value);
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value
        {
            get => GetAttribute("value");
            set => SetAttribute("value", value);
        }

        /// <summary>
        /// Gets or sets the character length for text, search, tel, url, email, or password.
        /// If no value is specified, the default value of 20 is used.
        /// </summary>
        public string Size
        {
            get => GetAttribute("size");
            set => SetAttribute("size", value);
        }

        /// <summary>
        /// Gets or sets whether the field is read-only.
        /// </summary>
        public string Readonly
        {
            get => GetAttribute("readonly");
            set => SetAttribute("readonly", value);
        }

        /// <summary>
        /// Gets or sets whether the input field can be used.
        /// </summary>
        public bool Disabled
        {
            get => HasAttribute("disabled");
            set { if (value) { SetAttribute("disabled"); } else { RemoveAttribute("disabled"); } }
        }

        /// <summary>
        /// Gets or sets the minimum value.
        /// </summary>
        public string Min
        {
            get => GetAttribute("min");
            set => SetAttribute("min", value);
        }

        /// <summary>
        /// Gets or sets the maximum value.
        /// </summary>
        public string Max
        {
            get => GetAttribute("max");
            set => SetAttribute("max", value);
        }

        /// <summary>
        /// Gets or sets the increment for numeric, date, or time indications. 
        /// </summary>
        public string Step
        {
            get => GetAttribute("step");
            set => SetAttribute("step", value);
        }

        /// <summary>
        /// Gets or sets the list (datalist).
        /// </summary>
        public string List
        {
            get => GetAttribute("list");
            set => SetAttribute("list", value);
        }

        /// <summary>
        /// Gets or sets whether multiple entries of file uploads and email inputs are possible.
        /// </summary>
        public string Multiple
        {
            get => GetAttribute("multiple");
            set => SetAttribute("multiple", value);
        }

        /// <summary>
        /// Gets or sets the minimum length.
        /// </summary>
        public string MinLength
        {
            get => GetAttribute("minlength");
            set => SetAttribute("minlength", value);
        }

        /// <summary>
        /// Gets or sets the maximum length.
        /// </summary>
        public string MaxLength
        {
            get => GetAttribute("maxlength");
            set => SetAttribute("maxlength", value);
        }

        /// <summary>
        /// Gets or sets whether inputs are enforced.
        /// </summary>
        public bool Required
        {
            get => HasAttribute("required");
            set { if (value) { SetAttribute("required"); } else { RemoveAttribute("required"); } }
        }

        /// <summary>
        /// Gets or sets whether a selection is made (only for radio and check).
        /// </summary>
        public bool Checked
        {
            get => HasAttribute("checked");
            set { if (value) { SetAttribute("checked"); } else { RemoveAttribute("checked"); } }
        }

        /// <summary>
        /// Gets or sets a search pattern that checks the content.
        /// </summary>
        public string Pattern
        {
            get => GetAttribute("pattern");
            set => SetAttribute("pattern", value);
        }

        /// <summary>
        /// Gets or sets a placeholder text.
        /// </summary>
        public string Placeholder
        {
            get => GetAttribute("placeholder");
            set => SetAttribute("placeholder", value);
        }

        /// <summary>
        /// Provides or sets the input method (helps mobile devices choose the correct keyboard layout).
        /// </summary>
        public string Inputmode
        {
            get => GetAttribute("inputmode");
            set => SetAttribute("inputmode", value);
        }

        /// <summary>
        /// Gets or sets the identification name of the form element to which it is associated.
        /// </summary>
        public string Form
        {
            get => GetAttribute("form");
            set => SetAttribute("form", value);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementFieldInput()
            : base("input")
        {
            CloseTag = false;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementFieldInput(params IHtmlNode[] nodes)
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
            base.ToString(builder, deep);
        }
    }
}
