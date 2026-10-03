using System;
using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents an embedded vector graphic.
    /// </summary>
    public class HtmlElementMultimediaSvg : HtmlElement, IHtmlElementMultimedia
    {
        /// <summary>
        /// Gets the elements.
        /// </summary>
        public new IEnumerable<IHtmlNode> Elements => base.Elements;

        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        public int Width
        {
            get => int.TryParse(GetAttribute("width"), out var width) ? width : 0;
            set => SetAttribute("width", value.ToString());
        }

        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        public int Height
        {
            get => int.TryParse(GetAttribute("height"), out var height) ? height : 0;
            set => SetAttribute("height", value.ToString());
        }

        /// <summary>
        /// Gets or sets the target.
        /// </summary>
        public string Target
        {
            get => GetAttribute("target");
            set => SetAttribute("target", value);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementMultimediaSvg()
            : base("svg")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="nodes">The content of the html element.</param>
        public HtmlElementMultimediaSvg(params IHtmlNode[] nodes)
            : this()
        {
            Add(nodes);
        }
    }
}
