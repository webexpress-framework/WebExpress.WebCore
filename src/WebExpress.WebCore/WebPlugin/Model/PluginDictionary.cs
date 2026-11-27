using System.Collections.Concurrent;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebPlugin.Model
{
    /// <summary>
    /// Directory of the registered plugins, keyed by plugin id.
    /// </summary>
    /// <remarks>
    /// Concurrent, because the package scan loads and unloads plugins while requests and the other
    /// managers look them up; a plain dictionary written during a read can throw or loop forever.
    /// </remarks>
    internal class PluginDictionary : ConcurrentDictionary<IComponentId, PluginItem>
    {
    }
}
