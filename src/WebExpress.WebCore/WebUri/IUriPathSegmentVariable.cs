using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A single part of a URI path (the pieces between the slashes) that is a placeholder rather than
    /// a fixed literal, such as <c>:id</c> in <c>/user/:id</c>. It captures the actual value from the
    /// request under a variable name and can restrict which values are accepted via a constraint expression.
    /// </summary>
    public interface IUriPathSegmentVariable : IUriPathSegment
    {
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        new string Value { get; set; }

        /// <summary>
        /// Gets the variable name.
        /// </summary>
        string VariableName { get; }

        /// <summary>
        /// Gets the regex expression.
        /// </summary>
        string Expression { get; }

        /// <summary>
        /// Creates a deep copy of the current path segment and assigns the specified value.
        /// </summary>
        /// <param name="value">
        /// The string value to assign to the copied segment.
        /// </param>
        /// <returns>
        /// A new instance representing the copied segment with the assigned value.
        /// </returns>
        IUriPathSegment Copy(string value);

        /// <summary>
        /// Returns a string that represents the display text for the current instance.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>
        /// A string containing the display text associated with the instance. The 
        /// value may be empty if no display text is available.
        /// </returns>
        string GetDisplayText(IRenderContext renderContext);

        /// <summary>
        /// Returns an icon that visually represents the parameter within the given render context.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information required to determine the appropriate icon.
        /// </param>
        /// <returns>
        /// An icon associated with the current instance. The value may be <c>null</c> or empty 
        /// if no icon is available.
        /// </returns>
        IIcon GetIcon(IRenderContext renderContext);
    }
}