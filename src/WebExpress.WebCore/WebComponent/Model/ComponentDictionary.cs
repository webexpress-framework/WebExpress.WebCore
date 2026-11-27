using System.Collections.Concurrent;
using System.Collections.Generic;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebComponent.Model
{
    /// <summary>
    /// Internal management of components.
    /// key = plugin
    /// value = component item
    /// </summary>
    /// <remarks>
    /// Concurrent, because plugins register their components while requests resolve managers; a
    /// list stored here is not changed after it is stored; a grown list replaces it.
    /// </remarks>
    internal class ComponentDictionary : ConcurrentDictionary<IPluginContext, List<ComponentItem>>
    {

    }
}
