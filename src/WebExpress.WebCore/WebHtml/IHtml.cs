using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Lowest-level building block of the HTML object model: anything that can render itself into
    /// HTML markup. Implementations write their output into a shared <see cref="StringBuilder"/>,
    /// which lets the whole page be assembled in a single buffer instead of concatenating strings.
    /// </summary>
    public interface IHtml
    {
        /// <summary>
        /// Convert to a string using a StringBuilder.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="deep">The call depth.</param>
        void ToString(StringBuilder builder, int deep);
    }
}
