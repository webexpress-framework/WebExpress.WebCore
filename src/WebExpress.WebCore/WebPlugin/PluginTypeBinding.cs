namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Says what an item of a <see cref="PluginTypeCatalog{TItem}"/> belongs to, because the
    /// framework's own extension points are scoped to an application while most extension
    /// points of an application are not, and the second kind must not pay for the first.
    /// </summary>
    public enum PluginTypeBinding
    {
        /// <summary>
        /// One item per type and plugin, shared by the whole host. A key names one item.
        /// </summary>
        Plugin,

        /// <summary>
        /// One item per type, plugin and application the plugin is associated with. A key names
        /// one item per application, and an application that leaves takes its items along.
        /// </summary>
        Application
    }
}
