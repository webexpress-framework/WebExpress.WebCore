using System;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// The starting segment of every URI path — the leading <c>/</c> from which all other segments
    /// branch. It is the entry point (id <c>ROOT</c>) of the path tree used for routing.
    /// </summary>
    public class UriPathSegmentRoot : IUriPathSegment
    {
        /// <summary>
        /// Gets the ID of the segment.
        /// </summary>
        public string Id => "ROOT";

        /// <summary>
        /// Gets or sets the path text.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the display text.
        /// </summary>
        public string Display { get; set; }

        /// <summary>
        /// Gets or sets the tag.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// Gets a value indicating whether the path segment is empty.
        /// </summary>
        public bool IsEmpty => false;

        /// <summary>
        /// Gets or sets a value indicating whether the item is hidden.
        /// </summary>
        /// <remarks>
        /// This property can be used to determine if the item should be displayed in user
        /// interfaces or lists.
        /// </remarks>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Gets or sets the URI to which the user is redirected.
        /// </summary>
        public IUri Uri { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UriPathSegmentRoot"/> class.
        /// </summary>
        /// <param name="display">The display text.</param>
        /// <param name="tag">The tag or null.</param>
        public UriPathSegmentRoot(string display = null, object tag = null)
        {
            Value = "/";
            Display = display;
            Tag = tag;
        }

        /// <summary>
        /// Checks whether the node matches the specified path element.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the path element matches, false otherwise.</returns>
        public bool IsMatched(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Value.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                   (Value + "/").Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a deep copy of the current segment.
        /// </summary>
        /// <returns>A copy of the current segment.</returns>
        public virtual IUriPathSegment Copy()
        {
            return new UriPathSegmentRoot(Display, Tag)
            {
                IsHidden = IsHidden,
                Uri = Uri
            };
        }

        /// <summary>
        /// Compares the current segment with another object.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns>True if the objects are equal, false otherwise.</returns>
        public virtual bool Equals(IUriPathSegment obj)
        {
            if (obj is null)
            {
                return false;
            }

            return obj is UriPathSegmentRoot;
        }

        /// <summary>
        /// Returns a string that represents the display text for the current instance.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>
        /// A string containing the display text associated with the instance. The 
        /// value may be empty if no display text is available.
        /// </returns>
        public virtual string GetDisplayText(IRenderContext renderContext)
        {
            return I18N.Translate(renderContext, Display);
        }

        /// <summary>
        /// Converts the segment to a string.
        /// </summary>
        /// <returns>A string that represents the current segment.</returns>
        public override string ToString()
        {
            return Value ?? "<null>";
        }
    }
}