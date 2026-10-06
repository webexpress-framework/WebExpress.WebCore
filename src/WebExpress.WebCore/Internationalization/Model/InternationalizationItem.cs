using System.Collections.Generic;

namespace WebExpress.WebCore.Internationalization.Model
{
    /// <summary>
    /// Holds the translations for a single language as a map from translation key to translated
    /// text. The internationalization manager keeps one of these per culture to look up localized strings.
    /// </summary>
    internal class InternationalizationItem : Dictionary<string, string>
    {
    }
}
