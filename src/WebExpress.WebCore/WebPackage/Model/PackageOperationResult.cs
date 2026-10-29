using System.Globalization;

namespace WebExpress.WebCore.WebPackage.Model
{
    /// <summary>
    /// Represents the result of a package lifecycle operation.
    /// </summary>
    public sealed class PackageOperationResult
    {
        private string _message;
        private PackageText _text;

        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets an optional operation message in the server's language. A caller that
        /// answers a request uses <see cref="GetMessage(CultureInfo)"/> instead.
        /// </summary>
        public string Message
        {
            get => _text?.ToString() ?? _message;
            set
            {
                _message = value;
                _text = null;
            }
        }

        /// <summary>
        /// Gets or sets the affected package, if available.
        /// </summary>
        public PackageCatalogItem Package { get; set; }

        /// <summary>
        /// Returns the operation message in the language of a culture.
        /// </summary>
        /// <param name="culture">The culture of the reader, or null for the server's default culture.</param>
        /// <returns>The message.</returns>
        public string GetMessage(CultureInfo culture)
        {
            return _text?.Translate(culture) ?? _message;
        }

        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        /// <param name="message">The result message.</param>
        /// <param name="package">The affected package.</param>
        /// <returns>The created operation result.</returns>
        public static PackageOperationResult Ok(string message, PackageCatalogItem package = null)
        {
            return new PackageOperationResult()
            {
                Success = true,
                Message = message,
                Package = package
            };
        }

        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        /// <param name="message">The result message.</param>
        /// <param name="package">The affected package.</param>
        /// <returns>The created operation result.</returns>
        public static PackageOperationResult Failed(string message, PackageCatalogItem package = null)
        {
            return new PackageOperationResult()
            {
                Success = false,
                Message = message,
                Package = package
            };
        }

        /// <summary>
        /// Creates a successful operation result whose message is translated for its reader.
        /// </summary>
        /// <param name="text">The result message.</param>
        /// <param name="package">The affected package.</param>
        /// <returns>The created operation result.</returns>
        internal static PackageOperationResult Ok(PackageText text, PackageCatalogItem package = null)
        {
            return new PackageOperationResult()
            {
                Success = true,
                _text = text,
                Package = package
            };
        }

        /// <summary>
        /// Creates a failed operation result whose message is translated for its reader.
        /// </summary>
        /// <param name="text">The result message.</param>
        /// <param name="package">The affected package.</param>
        /// <returns>The created operation result.</returns>
        internal static PackageOperationResult Failed(PackageText text, PackageCatalogItem package = null)
        {
            return new PackageOperationResult()
            {
                Success = false,
                _text = text,
                Package = package
            };
        }
    }
}
