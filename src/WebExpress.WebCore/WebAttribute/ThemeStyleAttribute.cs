using System;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Applied to a theme class to declare the CSS stylesheet that gives the theme its look. The
    /// constructor takes the URI of the stylesheet that should be loaded for the theme.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ThemeStyleAttribute : Attribute, IThemeAttribute
    {
        /// <summary>
        /// Initializes a new instance of the class with the specified URI.
        /// </summary>
        /// <param name="uri">The URI of the css theme style.</param>
        public ThemeStyleAttribute(string uri)
        {

        }
    }
}
