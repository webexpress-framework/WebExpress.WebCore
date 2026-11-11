using System;
using WebExpress.WebCore.WebApplication;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Describes where a discovered type was found, so a factory of a
    /// <see cref="PluginTypeCatalog{TItem}"/> can build an item that needs its context - its
    /// plugin, its application - instead of relying on a parameterless constructor.
    /// </summary>
    /// <param name="Type">The discovered type.</param>
    /// <param name="PluginContext">The plugin whose assembly holds the type, or <see langword="null"/>
    /// when an assembly was collected without a plugin.</param>
    /// <param name="ApplicationContext">The application the item is bound to, or <see langword="null"/>
    /// for a catalog bound per plugin.</param>
    public sealed record PluginTypeOrigin(Type Type, IPluginContext PluginContext, IApplicationContext ApplicationContext);
}
