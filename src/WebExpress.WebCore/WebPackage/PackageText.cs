using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.Internationalization;

namespace WebExpress.WebCore.WebPackage
{
    /// <summary>
    /// A message of the package manager, kept as key and arguments until someone reads it.
    /// </summary>
    /// <remarks>
    /// The package manager phrases a message without a request at hand - a scan records why a
    /// package waits long before a page shows it - so translating right away would fix the text
    /// to the server's language. Whoever shows the message translates it into the reader's.
    /// </remarks>
    internal sealed class PackageText
    {
        private const string KeyPrefix = "webexpress.webcore:packagemanager.";

        private readonly string _key;
        private readonly string _literal;
        private readonly object[] _args = [];
        private readonly PackageText[] _parts;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="key">The key below the package manager's internationalization prefix.</param>
        /// <param name="args">The formatting arguments; a package text among them is translated along.</param>
        public PackageText(string key, params object[] args)
        {
            _key = key;
            _args = args ?? [];
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="literal">The text that is shown as it is.</param>
        /// <param name="parts">The texts that are shown one after the other.</param>
        private PackageText(string literal, PackageText[] parts)
        {
            _literal = literal;
            _parts = parts;
        }

        /// <summary>
        /// Creates a text that is not translated, such as the message of an exception.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The package text.</returns>
        public static PackageText Literal(string text)
        {
            return new PackageText(text ?? string.Empty, null);
        }

        /// <summary>
        /// Creates a text that shows several texts one after the other.
        /// </summary>
        /// <param name="parts">The texts.</param>
        /// <returns>The package text.</returns>
        public static PackageText Join(IEnumerable<PackageText> parts)
        {
            return new PackageText(null, parts?.ToArray() ?? []);
        }

        /// <summary>
        /// Translates the text into the language of a culture.
        /// </summary>
        /// <param name="culture">The culture, or null for the server's default culture.</param>
        /// <returns>The translated text.</returns>
        public string Translate(CultureInfo culture)
        {
            if (_parts is not null)
            {
                return string.Join(" ", _parts.Select(x => x.Translate(culture)));
            }

            if (_literal is not null)
            {
                return _literal;
            }

            var args = _args.Select(x => x is PackageText text ? text.Translate(culture) : x).ToArray();

            return I18N.Translate(culture, KeyPrefix + _key, args);
        }

        /// <summary>
        /// Conversion into a string representation of the object.
        /// </summary>
        /// <returns>The text in the server's default culture.</returns>
        public override string ToString()
        {
            return Translate(null);
        }
    }
}
