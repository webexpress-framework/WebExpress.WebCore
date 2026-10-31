using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebResource.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application contexts, resource types, and resource items.
    /// key = plugin context
    /// value = { key = resource type, value = ressource item }
    /// </summary>
    internal class ResourceDictionary : Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, ResourceItem>>>
    {
        /// <summary>
        /// Adds a resource item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="resourceItem">The resource item.</param>
        /// <returns>True if the resource item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddResourceItem(IPluginContext pluginContext, IApplicationContext applicationContext, ResourceItem resourceItem)
        {
            var type = resourceItem.ResourceClass;

            if (!typeof(IResource).IsAssignableFrom(type))
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

            var resourceDict = appContextDict[applicationContext];

            if (!resourceDict.ContainsKey(type))
            {
                resourceDict[type] = resourceItem;
                return true;
            }

            return false; // item with the same resource class already exists
        }

        /// <summary>
        /// Removes a resource from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        public void RemoveResource<TResource>(IPluginContext pluginContext, IApplicationContext applicationContext)
            where TResource : IResource
        {
            var type = typeof(TResource);

            if (ContainsKey(pluginContext))
            {
                var appContextDict = this[pluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var resourceDict = appContextDict[applicationContext];

                    if (resourceDict.ContainsKey(type))
                    {
                        resourceDict.Remove(type);

                        if (resourceDict.Count == 0)
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
        /// Returns the resource items from the dictionary.
        /// </summary>
        /// <typeparam name="TResource">The type of resource.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of resource items</returns>
        public IEnumerable<ResourceItem> GetResourceItems<TResource>(IApplicationContext applicationContext)
            where TResource : IResource
        {
            return GetResourceItems(applicationContext, typeof(TResource));
        }

        /// <summary>
        /// Returns the resource items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="resourceType">The type of resource.</param>
        /// <returns>An IEnumerable of resource items</returns>
        public IEnumerable<ResourceItem> GetResourceItems(IApplicationContext applicationContext, Type resourceType)
        {
            if (!typeof(IResource).IsAssignableFrom(resourceType))
            {
                return [];
            }

            if (ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = this[applicationContext?.PluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var resourceDict = appContextDict[applicationContext];

                    if (resourceDict.ContainsKey(resourceType))
                    {
                        return [resourceDict[resourceType]];
                    }
                }
            }

            return [];
        }
    }
}
