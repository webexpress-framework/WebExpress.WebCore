using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A placeholder path segment that only matches when the value at that position is a GUID
    /// (for example <c>67d35a0f-7e94-4bfd-a309-36e9162a67ff</c>). Use it for routes keyed by a unique identifier.
    /// </summary>
    /// <typeparam name="TParameter">The parameter type.</typeparam>
    public class UriPathSegmentVariableGuid<TParameter> : UriPathSegmentVariable<TParameter>
        where TParameter : IParameterStatic, new()
    {
        /// <summary>
        /// The display formats of the guid.
        /// </summary>
        public enum Format
        {
            /// <summary>
            /// Full format of the guid.
            /// </summary>
            Full,

            /// <summary>
            /// Simple format of the guid.
            /// </summary>
            Simple
        }

        /// <summary>
        /// Gets the display format.
        /// </summary>
        public Format DisplayFormat { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="tag">The tag or null</param>
        public UriPathSegmentVariableGuid(object tag = null)
            : this(Format.Simple, tag)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="displayFormat">The display format.</param>
        /// <param name="tag">The tag or null</param>
        public UriPathSegmentVariableGuid(Format displayFormat, object tag = null)
            : base(tag)
        {
            DisplayFormat = displayFormat;
            Expression = @"^(\{){0,1}(([0-9a-fA-F]{8})\-([0-9a-fA-F]{4})\-([0-9a-fA-F]{4})\-([0-9a-fA-F]{4})\-([0-9a-fA-F]{12}))(\}){0,1}$";
        }

        /// <summary>
        /// Returns the variable.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The variable value pair.</returns>
        public override IDictionary<string, string> GetVariable(string value)
        {
            var match = UriPathSegmentRegexCache.Get(Expression).Match(value);

            if (match.Success)
            {
                var dict = new Dictionary<string, string>
                    {
                        { VariableName, match.Groups[2].ToString() }
                    };

                return dict;
            }

            return new Dictionary<string, string>();
        }

        /// <summary>
        /// Make a deep copy.
        /// </summary>
        /// <returns>The copy.</returns>
        public override IUriPathSegment Copy()
        {
            return new UriPathSegmentVariableGuid<TParameter>(DisplayFormat)
            {
                Expression = Expression,
                Value = Value,
                IsHidden = IsHidden,
                Uri = Uri
            };
        }

        /// <summary>
        /// Returns a string that represents the display text for the current instance.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>
        /// A string containing the display text associated with the instance. The 
        /// value may be empty if no display text is available.
        /// </returns>
        public override string GetDisplayText(IRenderContext renderContext)
        {
            if (Value is null)
            {
                return base.GetDisplayText(renderContext);
            }

            var match = UriPathSegmentRegexCache.Get(Expression).Match(Value);
            var guid = DisplayFormat == Format.Simple ? match.Groups[7].ToString() : match.Groups[2].ToString();

            if (string.IsNullOrWhiteSpace(Value) || !Value.Contains("{0}"))
            {
                return guid;
            }

            return string.Format
            (
                I18N.Translate(renderContext, Value),
                guid
            );
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