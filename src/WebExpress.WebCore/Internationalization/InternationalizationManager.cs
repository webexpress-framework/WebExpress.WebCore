using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using WebExpress.WebCore.Internationalization.Model;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.Internationalization
{
    /// <summary>
    /// Central registry for translations (internationalization, i18n). It collects the language
    /// files a plugin ships, and returns the text for a given key in the requested language so the
    /// UI can be shown in different languages.
    /// </summary>
    public sealed class InternationalizationManager : IInternationalizationManager, IComponentManagerPlugin, ISystemComponent
    {
        private readonly IComponentHub _componentHub;

        /// <summary>
        /// Gets the default language.
        /// </summary>
        public static CultureInfo DefaultCulture { get; private set; } = CultureInfo.CurrentCulture;

        /// <summary>
        /// Gets the directory by listing the internationalization key-value pairs.
        /// </summary>
        private static InternationalizationDictionary Dictionary { get; } = [];

        /// <summary>
        /// Gets or sets the reference to the context of the host.
        /// </summary>
        public IHttpServerContext HttpServerContext { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        private InternationalizationManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += (sender, pluginContext) =>
            {
                Register(pluginContext);
            };

            _componentHub?.PluginManager?.RemovePlugin += (sender, pluginContext) =>
            {
                Remove(pluginContext);
            };

            HttpServerContext = httpServerContext;
            DefaultCulture = HttpServerContext.Culture;

            HttpServerContext.Log?.Debug
            (
                Translate("webexpress.webcore:internationalizationmanager.initialization")
            );
        }

        /// <summary>
        /// Discovers and registers entries from the specified plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose elements are to be registered.</param>
        public void Register(IPluginContext pluginContext)
        {
            var pluginId = pluginContext.PluginId;
            Register(pluginContext.Assembly, pluginId.ToString());

            HttpServerContext.Log?.Debug
            (
                Translate("webexpress.webcore:internationalizationmanager.register", pluginId)
            );
        }

        /// <summary>
        /// Discovers and registers entries from the specified plugin.
        /// </summary>
        /// <param name="pluginContexts">A list with plugin contexts that contain the elements.</param>
        public void Register(IEnumerable<IPluginContext> pluginContexts)
        {
            foreach (var pluginContext in pluginContexts)
            {
                Register(pluginContext);
            }
        }

        /// <summary>
        /// Adds the internationalization key-value pairs from the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly that contains the key-value pairs to insert.</param>
        /// <param name="pluginId">The id of the plugin to which the internationalization data will be assigned.</param>
        public void Register(Assembly assembly, string pluginId)
        {
            var assemblyName = assembly.GetName().Name.ToLower();
            var name = assemblyName + ".internationalization.";
            var resources = assembly.GetManifestResourceNames().Where(x => x.Contains(name, System.StringComparison.CurrentCultureIgnoreCase));

            foreach (var languageResource in resources)
            {
                var language = languageResource.Split('.').LastOrDefault()?.ToLower();

                if (!Dictionary.TryGetValue(language, out InternationalizationItem value))
                {
                    value = ([]);
                    Dictionary.Add(language, value);
                }

                var dictItem = value;

                using var stream = assembly.GetManifestResourceStream(languageResource);
                using var streamReader = new StreamReader(stream);
                while (!streamReader.EndOfStream)
                {
                    var line = streamReader.ReadLine();
                    if (!line.StartsWith('#') && !string.IsNullOrWhiteSpace(line))
                    {
                        var split = line.Split('=');
                        var key = pluginId?.ToLower() + ":" + split[0]?.Trim().ToLower();

                        if (!dictItem.ContainsKey(key))
                        {
                            dictItem.Add(key, string.Join("=", split.Skip(1)));
                        }
                    }
                }
            }

            Log();
        }

        /// <summary>
        /// Removes all internationalization key-value pairs associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin containing the key-value pairs to remove.</param>
        public void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            foreach (var dictionary in Dictionary.Values)
            {
                var keysToRemove = dictionary.Keys.Where(k => k.StartsWith($"{pluginContext?.PluginId}:")).ToList();

                foreach (var key in keysToRemove)
                {
                    dictionary.Remove(key);
                }
            }

            Log();
        }

        /// <summary>
        /// Translates a given key to the default language.
        /// </summary>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(string key)
        {
            return Translate(DefaultCulture, null, key);
        }

        /// <summary>
        /// Translates a given key to the default language.
        /// </summary>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(string key, params object[] args)
        {
            return string.Format(Translate(DefaultCulture, null, key), args);
        }

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="request">The request with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(IRequest request, string key)
        {
            return Translate(request.Culture, null, key);
        }

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="request">The request with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(IRequest request, string key, params object[] args)
        {
            return string.Format(Translate(request, key), args);
        }

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(CultureInfo culture, string key)
        {
            return Translate(culture, null, key);
        }

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(CultureInfo culture, string key, params object[] args)
        {
            return string.Format(Translate(culture, key), args);
        }

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="pluginId">The plugin id.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(CultureInfo culture, string pluginId, string key)
        {
            var language = culture?.TwoLetterISOLanguageName;
            var k = string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(pluginId) || key.StartsWith($"{pluginId?.ToLower()}:") ? key?.ToLower() : $"{pluginId?.ToLower()}:{key?.ToLower()}";

            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(language) || language == "*")
            {
                language = DefaultCulture?.TwoLetterISOLanguageName;
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                return key;
            }

            if (Dictionary.TryGetValue(language, out InternationalizationItem item))
            {
                if (item.TryGetValue(k, out string value))
                {
                    return value;
                }
            }

            return key;
        }

        /// <summary>
        /// Translates a given key to the specified language.
        /// </summary>
        /// <param name="culture">The culture with the language to use.</param>
        /// <param name="pluginId">The plugin id.</param>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>The value of the key in the current language.</returns>
        public string Translate(CultureInfo culture, string pluginId, string key, params object[] args)
        {
            return string.Format(Translate(culture, pluginId, key), args);
        }

        /// <summary>
        /// Information about the component is collected and prepared for output in the log.
        /// </summary>
        private void Log()
        {

        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
