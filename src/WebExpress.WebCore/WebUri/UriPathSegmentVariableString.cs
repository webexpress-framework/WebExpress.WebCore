using System.Collections.Generic;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A placeholder path segment that matches any free-form text at that position (any value
    /// without a double quote). This is the default, least restrictive variable segment.
    /// </summary>
    /// <typeparam name="TParameter">The parameter type.</typeparam>
    public class UriPathSegmentVariableString<TParameter> : UriPathSegmentVariable<TParameter>
        where TParameter : IParameterStatic, new()
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="tag">The tag or null</param>
        public UriPathSegmentVariableString(object tag = null)
            : base(tag)
        {
            Expression = "^[^\"]*$";
            Tag = tag;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="segment">The path segment to copy.</param>
        public UriPathSegmentVariableString(UriPathSegmentVariableString<TParameter> segment)
            : base(segment.Tag)
        {
            Expression = segment.Expression;
        }

        /// <summary>
        /// Returns the variable.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The variable value pair.</returns>
        public override IDictionary<string, string> GetVariable(string value)
        {
            return new Dictionary<string, string>();
        }

        /// <summary>
        /// Make a deep copy.
        /// </summary>
        /// <returns>The copy.</returns>
        public override IUriPathSegment Copy()
        {
            return new UriPathSegmentVariableString<TParameter>(this)
            {
                Value = Value,
                IsHidden = IsHidden,
                Uri = Uri
            };
        }

        /// <summary>
        /// Converts the segment to a string.
        /// </summary>
        /// <returns>A string that represents the current segment.</returns>
        public override string ToString()
        {
            return base.ToString();
        }
    }
}