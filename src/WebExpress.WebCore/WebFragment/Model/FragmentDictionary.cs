using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebScope;

namespace WebExpress.WebCore.WebFragment.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application contexts,
    /// which in turn maps to a dictionary of section types and inner maps of scope types and lists of FragmentItem objects.
    /// Plugin -> Application -> Section -> Scope -> FragmentItem
    /// </summary>
    internal class FragmentDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, Dictionary<Type, List<FragmentItem>>>>> _dict = [];

        /// <summary>
        /// Gets all fragment contexts from the dictionary.
        /// </summary>
        public IEnumerable<IFragmentContext> All => _dict.Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x.Values)
            .SelectMany(x => x.Values)
            .SelectMany(x => x)
            .Select(x => x.FragmentContext);

        /// <summary>
        /// Adds a fragment item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="fragmentItem">The fragment item.</param>
        /// <returns>True if the fragment item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddFragmentItem(IPluginContext pluginContext, IApplicationContext applicationContext, FragmentItem fragmentItem)
        {
            var type = fragmentItem.FragmentClass;

            if (type.GetInterface(typeof(IFragment<,>).Name) is null)
            {
                return false;
            }

            if (!_dict.TryGetValue(pluginContext, out var applicationDict))
            {
                applicationDict = [];
                _dict[pluginContext] = applicationDict;
            }

            if (!applicationDict.TryGetValue(applicationContext, out var sectionDict))
            {
                sectionDict = [];
                applicationDict[applicationContext] = sectionDict;
            }

            if (!sectionDict.TryGetValue(fragmentItem.Section, out var scopeDict))
            {
                scopeDict = [];
                sectionDict[fragmentItem.Section] = scopeDict;
            }

            if (!scopeDict.TryGetValue(fragmentItem.Scope, out var itemList))
            {
                itemList = [];
                scopeDict[fragmentItem.Scope] = itemList;
            }

            if (!itemList.Any(x => x.FragmentClass == fragmentItem.FragmentClass))
            {
                itemList.Add(fragmentItem);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes fragments from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of fragment contexts that were removed.</returns>
        public IEnumerable<IFragmentContext> RemoveFragments(IPluginContext pluginContext)
        {
            var fragments = GetFragments(pluginContext);

            _dict.Remove(pluginContext);

            return fragments;
        }

        /// <summary>
        /// Removes fragments from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of fragment contexts that were removed.</returns>
        public IEnumerable<IFragmentContext> RemoveFragments(IApplicationContext applicationContext)
        {
            foreach (var pluginKeyValue in _dict)
            {
                if (pluginKeyValue.Value.TryGetValue(applicationContext, out var sectionDict))
                {
                    pluginKeyValue.Value.Remove(applicationContext);

                    if (pluginKeyValue.Value.Count == 0)
                    {
                        _dict.Remove(pluginKeyValue.Key);
                    }

                    foreach (var item in sectionDict.Values
                        .SelectMany(x => x.Values)
                        .SelectMany(x => x)
                        .Select(x => x.FragmentContext))
                    {
                        yield return item;
                    }
                }
            }
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <param name="scopes">The scopes where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<FragmentItem> GetFragmentItems(IApplicationContext applicationContext, Type section, IEnumerable<Type> scopes)
        {
            return _dict.Values
                .SelectMany(x => x)
                .Where(x => x.Key == applicationContext)
                .SelectMany(x => x.Value)
                .Where(x => x.Key == section || section.IsAssignableFrom(x.Key))
                .SelectMany(x => x.Value)
                .Where(x => scopes.Any(y => x.Key == y))
                .SelectMany(x => x.Value)
                .OrderBy(x => x.Order);
        }

        /// <summary>
        /// Returns all fragment items that belong to a given application context, fragment type, section, and scopes.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="fragment">The type of fragment.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <param name="scopes">The scopes where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragment items.</returns>
        public IEnumerable<FragmentItem> GetFragmentItems(IApplicationContext applicationContext, Type fragment, Type section, IEnumerable<Type> scopes)
        {
            return _dict.Values
                .SelectMany(x => x)
                .Where(x => x.Key == applicationContext)
                .SelectMany(x => x.Value)
                .Where(x => x.Key == section || section.IsAssignableFrom(x.Key))
                .SelectMany(x => x.Value)
                .Where(x => scopes.Any(y => x.Key == y))
                .SelectMany(x => x.Value)
                .Where(x => x.FragmentClass == fragment || fragment.IsAssignableFrom(x.FragmentClass))
                .OrderBy(x => x.Order);
        }

        /// <summary>
        /// Returns all fragment contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(IPluginContext pluginContext)
        {
            return _dict.Where(x => x.Key == pluginContext)
                .SelectMany(x => x.Value.Values)
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .SelectMany(x => x)
                .Select(x => x.FragmentContext);
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <param name="fragmentType">The fragment type.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(Type fragmentType)
        {
            return _dict.Values
                .SelectMany(x => x)
                .SelectMany(x => x.Value)
                .SelectMany(x => x.Value)
                .SelectMany(x => x.Value)
                .Where(x => x.FragmentClass == fragmentType)
                .OrderBy(x => x.Order)
                .Select(x => x.FragmentContext);
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="fragmentType">The fragment type.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type fragmentType)
        {
            return _dict.Values
                .SelectMany(x => x)
                .Where(x => x.Key == applicationContext)
                .SelectMany(x => x.Value)
                .SelectMany(x => x.Value)
                .SelectMany(x => x.Value)
                .Where(x => x.FragmentClass == fragmentType)
                .OrderBy(x => x.Order)
                .Select(x => x.FragmentContext);
        }

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <param name="scope">The scope where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        public IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type section, Type scope)
        {
            scope ??= typeof(IScope);

            return _dict.Values
                .SelectMany(x => x)
                .Where(x => x.Key == applicationContext)
                .SelectMany(x => x.Value)
                .Where(x => x.Key == section || section.IsAssignableFrom(x.Key))
                .SelectMany(x => x.Value)
                .Where(x => x.Key == scope)
                .SelectMany(x => x.Value)
                .OrderBy(x => x.Order)
                .Select(x => x.FragmentContext);
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

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <param name="applicationContext">The application context to check for.</param>
        /// <returns>True if the plugin context and application context exist in the dictionary, otherwise false.</returns>
        public bool Contains(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            return _dict.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext);
        }
    }
}
