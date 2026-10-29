using System.Collections.Concurrent;

namespace WebExpress.WebCore.Internationalization.Model
{
    /// <summary>
    /// key = language (ISO 639-1 two-letter)
    /// value = { key = , value = }
    /// </summary>
    internal class InternationalizationDictionary : ConcurrentDictionary<string, InternationalizationItem>
    {
    }
}
