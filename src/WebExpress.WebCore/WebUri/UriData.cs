namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A data url (RFC 2397), which carries its content instead of naming a location. It is kept
    /// as written: parsed as an endpoint it would lose everything but its scheme, and a picture
    /// sent inline would be served as an empty address.
    /// </summary>
    public class UriData : UriEndpoint
    {
        /// <summary>
        /// Gets the data url as written.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The data url, starting with "data:".</param>
        public UriData(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Returns the data url itself, which has no variable parts to fill.
        /// </summary>
        /// <param name="parameters">The parameters, which are ignored.</param>
        /// <returns>This data url.</returns>
        public override IUri BindParameters(params WebParameter.IParameter[] parameters)
        {
            return this;
        }

        /// <summary>
        /// Returns the data url as written.
        /// </summary>
        /// <returns>The data url.</returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
