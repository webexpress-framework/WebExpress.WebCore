using System.Collections.Generic;

namespace WebExpress.WebCore.WebPackage.Model
{
    /// <summary>
    /// Represents the result of package file validation.
    /// </summary>
    public sealed class PackageValidationResult
    {
        /// <summary>
        /// Gets or sets whether the package is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets validation messages.
        /// </summary>
        public List<string> Messages { get; } = [];

        /// <summary>
        /// Gets the validation messages untranslated, so an operation that fails on them can
        /// report them in the language of whoever asked for it.
        /// </summary>
        internal List<PackageText> Texts { get; } = [];

        /// <summary>
        /// Gets or sets metadata resolved from the package file.
        /// </summary>
        public PackageCatalogItem Package { get; set; }

        /// <summary>
        /// Adds a validation message.
        /// </summary>
        /// <param name="text">The message.</param>
        internal void Add(PackageText text)
        {
            Texts.Add(text);
            Messages.Add(text.ToString());
        }
    }
}
