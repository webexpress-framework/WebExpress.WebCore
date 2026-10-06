using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebIdentity.Model
{
    /// <summary>
    /// Internal lookup that stores the registered identity policies grouped first by plugin and then
    /// by application, so the manager can quickly find, add, or remove the policies that apply to a
    /// given application.
    /// </summary>
    internal class IdentityPolicyDictionary : Dictionary<IPluginContext, Dictionary<IApplicationContext, IList<IdentityPolicyItem>>>
    {
        /// <summary>
        /// Adds a policy item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="policyItem">The policy item.</param>
        /// <returns>True if the policy item was successfully added, false if an item with the same policy class already exists.</returns>
        public bool AddPolicyItem(IPluginContext pluginContext, IApplicationContext applicationContext, IdentityPolicyItem policyItem)
        {
            var type = policyItem.PolicyClass;

            if (!typeof(IIdentityPolicy).IsAssignableFrom(type))
            {
                return false;
            }

            if (!TryGetValue(pluginContext, out var appContextDict))
            {
                appContextDict = [];
                this[pluginContext] = appContextDict;
            }

            if (!appContextDict.TryGetValue(applicationContext, out var policyList))
            {
                policyList = [];
                appContextDict[applicationContext] = policyList;
            }

            if (policyList.Any(x => x.PolicyClass == type))
            {
                return false; // an item with the same policy class already exists
            }

            policyList.Add(policyItem);

            return true;
        }

        /// <summary>
        /// Removes a policy item from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        public void RemovePolicyItem<TIdentityPolicy>(IPluginContext pluginContext, IApplicationContext applicationContext)
            where TIdentityPolicy : IIdentityPolicy
        {
            var type = typeof(TIdentityPolicy);

            if (ContainsKey(pluginContext))
            {
                var appContextDict = this[pluginContext];

                if (appContextDict.TryGetValue(applicationContext, out var policyList))
                {
                    var itemToRemove = policyList.FirstOrDefault(x => x.PolicyClass == type);
                    if (itemToRemove is not null)
                    {
                        policyList.Remove(itemToRemove);

                        if (policyList.Count == 0)
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
        /// Returns the policy items from the dictionary.
        /// </summary>
        /// <typeparam name="TIdentityPolicy">The type of the policy.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of policy items</returns>
        public IEnumerable<IdentityPolicyItem> GetPolicyItems<TIdentityPolicy>(IApplicationContext applicationContext)
            where TIdentityPolicy : IIdentityPolicy
        {
            return GetPolicyItems(applicationContext, typeof(TIdentityPolicy));
        }

        /// <summary>
        /// Returns the policy items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="policyType">The type of the policy.</param>
        /// <returns>An IEnumerable of policy items</returns>
        public IEnumerable<IdentityPolicyItem> GetPolicyItems(IApplicationContext applicationContext, Type policyType)
        {
            if (!typeof(IIdentityPolicy).IsAssignableFrom(policyType))
            {
                return [];
            }

            if (ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = this[applicationContext?.PluginContext];

                if (appContextDict.TryGetValue(applicationContext, out var policyList))
                {
                    return policyList.Where(x => x.PolicyClass == policyType);
                }
            }

            return [];
        }

        /// <summary>
        /// Returns all policy contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of policy contexts.</returns>
        public IEnumerable<IIdentityPolicyContext> GetPolicyContexts(IPluginContext pluginContext)
        {
            return this.Where(x => x.Key == pluginContext)
                .SelectMany(x => x.Value)
                .SelectMany(x => x.Value)
                .Select(x => x.PolicyContext);
        }

        /// <summary>
        /// Returns all policy contexts for a given application context.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of policy contexts.</returns>
        public IEnumerable<IIdentityPolicyContext> GetPolicyContexts(IApplicationContext applicationContext)
        {
            return Values.SelectMany(x => x)
                .Where(x => x.Key == applicationContext)
                .SelectMany(entry => entry.Value)
                .Select(x => x.PolicyContext);
        }
    }
}
