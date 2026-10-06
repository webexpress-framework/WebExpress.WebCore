using System;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A fixed-literal part of a URI path, such as <c>ix</c> in <c>/ix/home</c>. During routing it
    /// matches a request only when that part of the path is exactly equal (case-insensitively).
    /// </summary>
    public class UriPathSegmentConstant : IUriPathSegmentConstant
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id => Value?.ToLower();

        /// <summary>
        /// Gets or sets the path text.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the tag.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// Checks for empty path segment.
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value) || Value.Equals("/");

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
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The name.</param>
        /// <param name="tag">The tag or null</param>
        public UriPathSegmentConstant(string value, object tag = null)
        {
            Value = value;
            Tag = tag;
        }

        /// <summary>
        /// Checks whether the node matches the path element.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the path element matched, false otherwise.</returns>
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
        /// Make a deep copy.
        /// </summary>
        /// <returns>The copy.</returns>
        public virtual IUriPathSegment Copy()
        {
            return new UriPathSegmentConstant(Value, Tag)
            {
                IsHidden = IsHidden,
                Uri = Uri
            };
        }

        /// <summary>
        /// Compare the object.
        /// </summary>
        /// <param name="obj">The comparison object.</param>
        /// <returns>true if equals, false otherwise</returns>
        public virtual bool Equals(IUriPathSegment obj)
        {
            if (obj is null)
            {
                return false;
            }
            else if (obj is UriPathSegmentConstant segment)
            {
                return Value.Equals(segment.Value, StringComparison.OrdinalIgnoreCase);
            }

            return false;
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
            return null;
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