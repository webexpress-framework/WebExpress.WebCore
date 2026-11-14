using System;
using WebExpress.WebCore.WebApplication;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Reports a change of the item in use under a key of a <see cref="PluginTypeCatalog{TItem}"/>,
    /// so a manager can raise its own events or drop what it derived from the item without
    /// comparing snapshots.
    /// </summary>
    /// <typeparam name="TItem">The type of the catalog's items.</typeparam>
    public sealed class PluginTypeCatalogEventArgs<TItem> : EventArgs
    {
        /// <summary>
        /// Gets the key the item is filed under.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the item that started or stopped being the one in use.
        /// </summary>
        public TItem Item { get; }

        /// <summary>
        /// Gets the plugin that offers the item, or <see langword="null"/> for an item given to
        /// <see cref="PluginTypeCatalog{TItem}.Register(TItem)"/> or collected from an assembly
        /// without a plugin.
        /// </summary>
        public IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the application the item is bound to, or <see langword="null"/> for a catalog
        /// bound per plugin.
        /// </summary>
        public IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Captures one change, because the catalog raises its events after it released its lock
        /// and the state may have moved on by the time a handler runs.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="item">The item.</param>
        /// <param name="pluginContext">The offering plugin.</param>
        /// <param name="applicationContext">The bound application.</param>
        public PluginTypeCatalogEventArgs(string key, TItem item, IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            Key = key;
            Item = item;
            PluginContext = pluginContext;
            ApplicationContext = applicationContext;
        }
    }
}
