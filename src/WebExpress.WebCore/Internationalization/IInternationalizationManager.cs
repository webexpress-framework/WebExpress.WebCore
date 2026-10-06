using System.Globalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Internationalization
{
    /// <summary>
    /// Contract for the translation registry. It looks up the text for a given key in a chosen (or
    /// the default) language, so callers can display localized strings without knowing where the
    /// translations come from.
    /// </summary>
    public interface IInternationalizationManager : IComponentManager
    {
        /// <summary>
        /// Translates a given key to the default language.
        /// </summary>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(string key);

        /// <summary>
        /// Translates a given key to the default language.
        /// </summary>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(string key, params object[] args);

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="request">The request with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        string Translate(IRequest request, string key);

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="request">The request with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        string Translate(IRequest request, string key, params object[] args);

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        string Translate(CultureInfo culture, string key);

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        string Translate(CultureInfo culture, string key, params object[] args);

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="pluginId">The plugin id.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        string Translate(CultureInfo culture, string pluginId, string key);

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="pluginId">The plugin id.</param>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        string Translate(CultureInfo culture, string pluginId, string key, params object[] args);
    }
}
