using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a dialog box or other interactive component, such as a dismissible alert or subwindow.
    /// </summary>
    public class HtmlElementInteractiveDialog : HtmlElement, IHtmlElementInteractive
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Gets or sets a value indicating whether the dialog is active and available for interaction.
        /// </summary>
        public bool Open
        {
            get => HasUserAttribute("open");
            set { if (value) { AddUserAttribute("open"); } else { RemoveUserAttribute("open"); } }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementInteractiveDialog()
            : base("dialog")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementInteractiveDialog(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
