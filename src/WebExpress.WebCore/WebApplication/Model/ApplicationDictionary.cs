using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebApplication.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application items.
    /// </summary>
    internal class ApplicationDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<string, ApplicationItem>> _dict = [];

        /// <summary>
        /// Gets all application contexts from the dictionary.
        /// </summary>
        public IEnumerable<IApplicationContext> All => _dict
            .Values.SelectMany(x => x.Values)
            .Select(x => x.ApplicationContext);

        /// <summary>
        /// Adds an application item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationItem">The application item.</param>
        /// <returns>True if the application item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddApplication(IPluginContext pluginContext, ApplicationItem applicationItem)
        {
            if (!_dict.TryGetValue(pluginContext, out var applicationDict))
            {
                applicationDict = [];
                _dict[pluginContext] = applicationDict;
            }

            if (applicationDict.TryAdd(applicationItem.ApplicationContext.ApplicationId, applicationItem))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes applications from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The items of the removed applications, so the caller can raise removal events and release the instances.</returns>
        public IEnumerable<ApplicationItem> RemoveApplications(IPluginContext pluginContext)
        {
            // removal events need the contexts after the registry entry has been deleted
            var applicationItems = GetApplicationItems(pluginContext).ToArray();

            _dict.Remove(pluginContext);

            return applicationItems;
        }

        /// <summary>
        /// Returns the item carrying a given application context.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>The item, or null when the context belongs to no registered application.</returns>
        public ApplicationItem GetApplicationItem(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return null;
            }

            return _dict
                .Values.SelectMany(x => x.Values)
                .FirstOrDefault(x => x.ApplicationContext == applicationContext);
        }

        /// <summary>
        /// Returns the application contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of application contexts associated with the given plugin context.</returns>
        public IEnumerable<ApplicationItem> GetApplicationItems(IPluginContext pluginContext)
        {
            var applicationItems = _dict
                .Where(x => x.Key == pluginContext)
                .Select(x => x.Value)
                .Select(x => x.Values)
                .SelectMany(x => x);

            return applicationItems;
        }

        /// <summary>
        /// Returns the application contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of application contexts associated with the given plugin context.</returns>
        public IEnumerable<IApplicationContext> GetApplications(IPluginContext pluginContext)
        {
            var applicationContexts = GetApplicationItems(pluginContext)
                .Select(x => x.ApplicationContext);

            return applicationContexts;
        }

        /// <summary>
        /// Returns the application context for a given application id.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <returns>The context of the application or null if the application id is null, empty, or not found.</returns>
        public IApplicationContext GetApplication(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId)) return null;

            var items = _dict.Values
                .Where(x => x.ContainsKey(applicationId.ToLower()))
                .Select(x => x[applicationId.ToLower()])
                .FirstOrDefault();

            return items?.ApplicationContext;
        }

        /// <summary>
        /// Returns the application contexts for a given application type.
        /// </summary>
        /// <param name="application">The application type.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        public IEnumerable<IApplicationContext> GetApplications(Type application)
        {
            if (application is null) return [];

            var items = _dict.Values.SelectMany(x => x.Values)
                .Where(x => x.ApplicationClass.Equals(application) || application.IsAssignableFrom(x.ApplicationClass))
                .Select(x => x.ApplicationContext);

            return items;
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <returns>True if the plugin context exists in the dictionary, otherwise false.</returns>
        public bool Contains(IPluginContext pluginContext)
        {
            return _dict.ContainsKey(pluginContext);
        }
    }
}
