namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Specifies how the data is encoded when it is transferred to the server.
    /// </summary>
    public enum TypeEnctype
    {
        /// <summary>
        /// All characters are encoded (spaces are converted to "+" and special characters to hex representation).
        /// </summary>
        UrLEncoded,

        /// <summary>
        /// Multipart form data (used for file uploads and FormData).
        /// </summary>
        Multipart,

        /// <summary>
        /// Only space characters are encoded.
        /// </summary>
        Text,

        /// <summary>
        /// No characters will be encoded.
        /// </summary>
        None,

        /// <summary>
        /// Not assignable.
        /// </summary>
        Default

    }

    /// <summary>
    /// Provides extension methods for the TypeEnctype enumeration.
    /// </summary>
    public static class TypeEnctypeExtensions
    {
        /// <summary>
        /// Conversion of string to TypeEnctype.
        /// </summary>
        /// <param name="enctype">The coding.</param>
        /// <returns>The converted encoding.</returns>
        public static TypeEnctype Convert(string enctype)
        {
            if (string.IsNullOrWhiteSpace(enctype))
            {
                return TypeEnctype.Default;
            }

            var ct = enctype.ToLowerInvariant();

            if (ct.StartsWith("multipart/form-data"))
            {
                return TypeEnctype.Multipart;
            }

            return ct switch
            {
                "text/plain" => TypeEnctype.Text,
                "application/x-www-form-urlencoded" => TypeEnctype.UrLEncoded,
                _ => TypeEnctype.Default,
            };

        }

        /// <summary>
        /// Converts the encoding to its string representation.
        /// </summary>
        /// <param name="enctype">The coding.</param>
        /// <returns>The converted encoding.</returns>
        public static string Convert(this TypeEnctype enctype)
        {
            return enctype switch
            {
                TypeEnctype.Multipart => "multipart/form-data",
                TypeEnctype.Text => "text/plain",
                TypeEnctype.UrLEncoded => "application/x-www-form-urlencoded",
                TypeEnctype.None => string.Empty,
                _ => string.Empty
            };
        }
    }
}
