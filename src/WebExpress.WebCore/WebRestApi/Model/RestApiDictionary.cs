using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebRestApi.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application contexts, rest api types, and rest api items.
    /// key = plugin context
    /// value = { key = rest api id, value = rest api item }
    /// </summary>
    internal class RestApiDictionary : Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, RestApiItem>>>
    {
        /// <summary>
        /// Adds a rest api item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="restApiItem">The rest api item.</param>
        /// <returns>True if the rest api item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddRestApiItem(IPluginContext pluginContext, IApplicationContext applicationContext, RestApiItem restApiItem)
        {
            var type = restApiItem.RestApiClass;

            if (!typeof(IRestApi).IsAssignableFrom(type))
            {
                return false;
            }

            if (!ContainsKey(pluginContext))
            {
                this[pluginContext] = [];
            }

            var appContextDict = this[pluginContext];

            if (!appContextDict.ContainsKey(applicationContext))
            {
                appContextDict[applicationContext] = [];
            }

            var restApiDict = appContextDict[applicationContext];

            if (!restApiDict.ContainsKey(type))
            {
                restApiDict[type] = restApiItem;
                return true;
            }

            return false; // item with the same rest api class already exists
        }

        /// <summary>
        /// Removes a rest api from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        public void RemoveRestApi<TRestApi>(IPluginContext pluginContext, IApplicationContext applicationContext)
            where TRestApi : IRestApi
        {
            var type = typeof(TRestApi);

            if (ContainsKey(pluginContext))
            {
                var appContextDict = this[pluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var restApiDict = appContextDict[applicationContext];

                    if (restApiDict.ContainsKey(type))
                    {
                        restApiDict.Remove(type);

                        if (restApiDict.Count == 0)
                        {
                            appContextDict.Remove(applicationContext);

                            if (appContextDict.Count == 0)
                            {
                                Remove(pluginContext);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the rest api items from the dictionary.
        /// </summary>
        /// <typeparam name="TRestApi">The type of rest api.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of rest api items</returns>
        public IEnumerable<RestApiItem> GetRestApiItems<TRestApi>(IApplicationContext applicationContext)
            where TRestApi : IRestApi
        {
            return GetRestApiItems(applicationContext, typeof(TRestApi));
        }

        /// <summary>
        /// Returns the rest api items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="restApiType">The type of rest api.</param>
        /// <returns>An IEnumerable of rest api items</returns>
        public IEnumerable<RestApiItem> GetRestApiItems(IApplicationContext applicationContext, Type restApiType)
        {
            if (!typeof(IRestApi).IsAssignableFrom(restApiType))
            {
                return [];
            }

            if (ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = this[applicationContext?.PluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var restApiDict = appContextDict[applicationContext];

                    if (restApiDict.ContainsKey(restApiType))
                    {
                        return [restApiDict[restApiType]];
                    }
                }
            }

            return [];
        }
    }
}
