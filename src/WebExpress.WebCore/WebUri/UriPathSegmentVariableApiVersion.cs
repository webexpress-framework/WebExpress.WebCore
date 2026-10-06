using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A placeholder path segment that captures the API version number from the path
    /// (the <c>1</c> in <c>/api/1/...</c>), so REST endpoints can be routed per version.
    /// </summary>
    /// <typeparam name="TParameter">The parameter type.</typeparam>
    internal class UriPathSegmentVariableApiVersion<TParameter> : UriPathSegmentVariable<TParameter>
        where TParameter : IParameterStatic, new()
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The value.</param>
        public UriPathSegmentVariableApiVersion(string value)
            : base()
        {
            Value = value;
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
        /// Checks whether the node matches the path element.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the path element matched, false otherwise.</returns>
        public override bool IsMatched(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            else if (value.Equals(Value))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Make a deep copy.
        /// </summary>
        /// <returns>The copy.</returns>
        public override IUriPathSegment Copy()
        {
            return new UriPathSegmentVariableApiVersion<TParameter>(Value)
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
        public override bool Equals(IUriPathSegment obj)
        {
            if (obj is null)
            {
                return false;
            }
            else if (obj is UriPathSegmentVariable<TParameter> segment)
            {
                return VariableName.Equals(segment.VariableName, StringComparison.OrdinalIgnoreCase)
                    && Value.Equals(segment.Value);
            }

            return false;
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