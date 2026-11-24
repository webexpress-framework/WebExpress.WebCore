using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebApplication.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application items.
    /// </summary>
    /// <remarks>
    /// The package scan adds and removes applications on its own thread while requests resolve
    /// them, and a dictionary written during a read can throw or loop forever. Every member
    /// therefore runs under one lock and returns a list taken under it. Application ids compare
    /// ordinally ignoring case, which does not depend on the culture of the calling thread.
    /// </remarks>
    internal class ApplicationDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<string, ApplicationItem>> _dict = [];
        private readonly Lock _lock = new();

        /// <summary>
        /// Gets all application contexts from the dictionary.
        /// </summary>
        public IEnumerable<IApplicationContext> All
        {
            get
            {
                lock (_lock)
                {
                    return [.. _dict.Values.SelectMany(x => x.Values).Select(x => x.ApplicationContext)];
                }
            }
        }

        /// <summary>
        /// Adds an application item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationItem">The application item.</param>
        /// <returns>True if the application item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddApplication(IPluginContext pluginContext, ApplicationItem applicationItem)
        {
            lock (_lock)
            {
                if (!_dict.TryGetValue(pluginContext, out var applicationDict))
                {
                    applicationDict = new Dictionary<string, ApplicationItem>(StringComparer.OrdinalIgnoreCase);
                    _dict[pluginContext] = applicationDict;
                }

                return applicationDict.TryAdd(applicationItem.ApplicationContext.ApplicationId, applicationItem);
            }
        }

        /// <summary>
        /// Removes applications from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>The items of the removed applications, so the caller can raise removal events and release the instances.</returns>
        public IEnumerable<ApplicationItem> RemoveApplications(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dict.Remove(pluginContext, out var applicationDict)
                    ? [.. applicationDict.Values]
                    : [];
            }
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

            lock (_lock)
            {
                return _dict
                    .Values.SelectMany(x => x.Values)
                    .FirstOrDefault(x => x.ApplicationContext == applicationContext);
            }
        }

        /// <summary>
        /// Returns the application contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of application contexts associated with the given plugin context.</returns>
        public IEnumerable<ApplicationItem> GetApplicationItems(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return [];
            }

            lock (_lock)
            {
                return _dict.TryGetValue(pluginContext, out var applicationDict) ? [.. applicationDict.Values] : [];
            }
        }

        /// <summary>
        /// Returns the application contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of application contexts associated with the given plugin context.</returns>
        public IEnumerable<IApplicationContext> GetApplications(IPluginContext pluginContext)
        {
            return [.. GetApplicationItems(pluginContext).Select(x => x.ApplicationContext)];
        }

        /// <summary>
        /// Returns the application context for a given application id.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <returns>The context of the application or null if the application id is null, empty, or not found.</returns>
        public IApplicationContext GetApplication(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
            {
                return null;
            }

            lock (_lock)
            {
                foreach (var applicationDict in _dict.Values)
                {
                    if (applicationDict.TryGetValue(applicationId, out var item))
                    {
                        return item.ApplicationContext;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Returns the application contexts for a given application type.
        /// </summary>
        /// <param name="application">The application type.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        public IEnumerable<IApplicationContext> GetApplications(Type application)
        {
            if (application is null)
            {
                return [];
            }

            lock (_lock)
            {
                return [.. _dict.Values.SelectMany(x => x.Values)
                    .Where(x => x.ApplicationClass.Equals(application) || application.IsAssignableFrom(x.ApplicationClass))
                    .Select(x => x.ApplicationContext)];
            }
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <returns>True if the plugin context exists in the dictionary, otherwise false.</returns>
        public bool Contains(IPluginContext pluginContext)
        {
            lock (_lock)
            {
                return pluginContext is not null && _dict.ContainsKey(pluginContext);
            }
        }
    }
}
