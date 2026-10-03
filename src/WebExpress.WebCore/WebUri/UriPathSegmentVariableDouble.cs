using System.Collections.Generic;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A placeholder path segment that only matches when the value at that position is a decimal
    /// number. Use it to restrict a route parameter to floating-point values.
    /// </summary>
    /// <typeparam name="TParameter">The parameter type.</typeparam>
    public class UriPathSegmentVariableDouble<TParameter> : UriPathSegmentVariable<TParameter>
        where TParameter : IParameterStatic, new()
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="tag">The tag or null</param>
        public UriPathSegmentVariableDouble(object tag = null)
            : base(tag)
        {
            Expression = @"^[+-]?(\d*,\d+|\d+(,\d*)?)([eE][+-]?\d+)?$";
            Tag = tag;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="segment">The path segment to copy.</param>
        public UriPathSegmentVariableDouble(UriPathSegmentVariableDouble<TParameter> segment)
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
            return new UriPathSegmentVariableDouble<TParameter>(this)
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