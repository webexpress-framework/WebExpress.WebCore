using System;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents an area of bitmap that can be used by scripts to dynamically display 
    /// diagrams, game graphics, or other visual effects, for example.
    /// </summary>
    public class HtmlElementScriptingCanvas : HtmlElement, IHtmlElementScripting
    {
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
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementScriptingCanvas()
            : base("canvas")
        {
        }
    }
}
