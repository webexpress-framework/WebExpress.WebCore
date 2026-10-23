using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// The query part (e.g. ?title=Uniform_Resource_Identifier).
    /// </summary>
    public class UriQuery : IUriQuery
    {
        /// <summary>
        /// Gets the key.
        /// </summary>
        public string Key { get; protected set; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public UriQuery(string key, string value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>
        /// Converts the query to a string.
        /// </summary>
        /// <returns>A string that represents the current query.</returns>
        public override string ToString()
        {
            return string.Concat(Key, "=", Value);
        }
    }

    /// <summary>
    /// Represents a key-value pair used as a query parameter in a URI.
    /// </summary>
    /// <typeparam name="TParameter">
    /// The type that implements the IParameterStatic interface and defines the structure 
    /// of the query parameter.
    /// </typeparam>
    public class UriQuery<TParameter> : IUriQuery
        where TParameter : IParameterStatic
    {
        /// <summary>
        /// Returns the key.
        /// </summary>
        public string Key { get; protected set; }

        /// <summary>
        /// Returns the value.
        /// </summary>
        public string Value { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The value.</param>
        public UriQuery(string value = null)
        {
            Key = TParameter.Key;
            Value = value;
        }

        /// <summary>
        /// Converts the query to a string.
        /// </summary>
        /// <returns>A string that represents the current query.</returns>
        public override string ToString()
        {
            return string.Concat(Key, "=", Value);
        }
    }
}