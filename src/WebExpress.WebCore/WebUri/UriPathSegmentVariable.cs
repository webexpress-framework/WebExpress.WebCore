using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// Caches compiled regular expressions keyed by their pattern so that route matching does not
    /// recompile the same expression on every request. Compilation is comparatively expensive, while
    /// matching against an already compiled instance is fast, which matters on the request hot path.
    /// </summary>
    internal static class UriPathSegmentRegexCache
    {
        private static readonly ConcurrentDictionary<string, Regex> _cache = new();

        /// <summary>
        /// Returns a compiled regular expression for the given pattern, reusing a cached instance when available.
        /// </summary>
        /// <param name="pattern">The regular expression pattern.</param>
        /// <returns>A compiled, case-insensitive regular expression.</returns>
        public static Regex Get(string pattern)
        {
            return _cache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }
    }

    /// <summary>
    /// Base class for placeholder path segments such as <c>:id</c> in <c>/user/:id</c>. It captures
    /// the value found at that position under a variable name and matches it against an optional
    /// constraint expression. Concrete subclasses (int, double, GUID, string, regex, …) supply the
    /// constraint for a particular value type; <typeparamref name="TParameter"/> ties the captured
    /// value to the strongly typed request parameter it represents.
    /// </summary>
    /// <typeparam name="TParameter">The parameter type.</typeparam>
    public abstract class UriPathSegmentVariable<TParameter> : IUriPathSegmentVariable
        where TParameter : IParameterStatic, new()
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id => VariableName?.ToLower();

        /// <summary>
        /// Gets or sets the variable name.
        /// </summary>
        public string VariableName { get; set; }

        /// <summary>
        /// Gets or sets the path text.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the regex expression.
        /// </summary>
        public string Expression { get; protected set; }

        /// <summary>
        /// Gets or sets the tag.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// Checks for empty path segment.
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(VariableName) || VariableName.Equals("/");

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
        /// <param name="tag">The tag or null</param>
        public UriPathSegmentVariable(object tag = null)
        {
            VariableName = TParameter.Key;
            Tag = tag;
        }

        /// <summary>
        /// Returns the variable.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The variable value pair.</returns>
        public abstract IDictionary<string, string> GetVariable(string value);

        /// <summary>
        /// Checks whether the node matches the path element.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the path element matched, false otherwise.</returns>
        public virtual bool IsMatched(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // without a constraint expression the segment can only be matched by a literal value
            if (string.IsNullOrEmpty(Expression))
            {
                return Value is not null && Value.Equals(value, StringComparison.OrdinalIgnoreCase);
            }

            return UriPathSegmentRegexCache.Get(Expression).IsMatch(value);
        }

        /// <summary>
        /// Make a deep copy.
        /// </summary>
        /// <returns>The copy.</returns>
        public abstract IUriPathSegment Copy();

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
            else if (obj is UriPathSegmentVariable<TParameter> segment)
            {
                return VariableName.Equals(segment.VariableName, StringComparison.OrdinalIgnoreCase)
                    && (
                        (Expression is null && segment.Expression is null)
                        || Expression.Equals(segment.Expression)
                    );
            }

            return false;
        }

        /// <summary>
        /// Creates a deep copy of the current path segment and assigns the specified value.
        /// </summary>
        /// <param name="value">
        /// The string value to assign to the copied segment.
        /// </param>
        /// <returns>
        /// A new instance representing the copied segment with the assigned value.
        /// </returns>
        public IUriPathSegment Copy(string value)
        {
            var copy = Copy();
            if (copy is UriPathSegmentVariable<TParameter> segment)
            {
                segment.Value = value;
            }

            return copy;
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
            return Value;
        }

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
        public virtual IIcon GetIcon(IRenderContext renderContext)
        {
            return null;
        }

        /// <summary>
        /// Converts the segment to a string.
        /// </summary>
        /// <returns>A string that represents the current segment.</returns>
        public override string ToString()
        {
            return Value ?? $"${{{VariableName}}}";
        }
    }
}