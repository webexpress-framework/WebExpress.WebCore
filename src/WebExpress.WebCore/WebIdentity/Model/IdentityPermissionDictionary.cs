using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebIdentity.Model
{
    /// <summary>
    /// Internal lookup that stores the registered identity permissions grouped first by plugin and
    /// then by application, so the manager can quickly find, add, or remove the permissions that
    /// apply to a given application.
    /// </summary>
    internal class IdentityPermissionDictionary : Dictionary<IPluginContext, Dictionary<IApplicationContext, IList<IdentityPermissionItem>>>
    {
        /// <summary>
        /// Adds a permission item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="permissionItem">The permission item.</param>
        /// <returns>True if the permission item was successfully added, false if an item with the same permission class already exists.</returns>
        public bool AddPermissionItem(IPluginContext pluginContext, IApplicationContext applicationContext, IdentityPermissionItem permissionItem)
        {
            var type = permissionItem.PermissionClass;

            if (!typeof(IIdentityPermission).IsAssignableFrom(type))
            {
                return false;
            }

            if (!TryGetValue(pluginContext, out var appContextDict))
            {
                appContextDict = new Dictionary<IApplicationContext, IList<IdentityPermissionItem>>();
                this[pluginContext] = appContextDict;
            }

            if (!appContextDict.TryGetValue(applicationContext, out var permissionList))
            {
                permissionList = new List<IdentityPermissionItem>();
                appContextDict[applicationContext] = permissionList;
            }

            if (permissionList.Any(x => x.PermissionClass == type))
            {
                return false; // an item with the same permission class already exists
            }

            permissionList.Add(permissionItem);

            return true;
        }

        /// <summary>
        /// Removes a permission item from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        public void RemovePermissionItem<TIdentityPermission>(IPluginContext pluginContext, IApplicationContext applicationContext)
            where TIdentityPermission : IIdentityPermission
        {
            var type = typeof(TIdentityPermission);

            if (ContainsKey(pluginContext))
            {
                var appContextDict = this[pluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var permissionList = appContextDict[applicationContext];

                    var itemToRemove = permissionList.FirstOrDefault(x => x.PermissionClass == type);
                    if (itemToRemove is not null)
                    {
                        permissionList.Remove(itemToRemove);

                        if (permissionList.Count == 0)
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
        /// Returns the permission items from the dictionary.
        /// </summary>
        /// <typeparam name="TIdentityPermission">The type of the permission.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of permission items</returns>
        public IEnumerable<IdentityPermissionItem> GetPermissionItems<TIdentityPermission>(IApplicationContext applicationContext)
            where TIdentityPermission : IIdentityPermission
        {
            return GetPermissionItems(applicationContext, typeof(TIdentityPermission));
        }

        /// <summary>
        /// Returns the permission items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="permissionType">The type of the permission.</param>
        /// <returns>An IEnumerable of permission items</returns>
        public IEnumerable<IdentityPermissionItem> GetPermissionItems(IApplicationContext applicationContext, Type permissionType)
        {
            if (!typeof(IIdentityPermission).IsAssignableFrom(permissionType))
            {
                return [];
            }

            if (ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = this[applicationContext?.PluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var permissionList = appContextDict[applicationContext];

                    return permissionList.Where(x => x.PermissionClass == permissionType);
                }
            }

            return Enumerable.Empty<IdentityPermissionItem>();
        }

        /// <summary>
        /// Returns all permission contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of permission contexts.</returns>
        public IEnumerable<IApplicationContext> GetPermissionContexts(IPluginContext pluginContext)
        {
            return this.Where(entry => entry.Key == pluginContext)
                       .SelectMany(entry => entry.Value.Keys);
        }
    }
}
