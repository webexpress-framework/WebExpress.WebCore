using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebEvent
{
    /// <summary>
    /// Read-only descriptor of a registered event handler, exposing its id and the application and
    /// plugin it belongs to, so the event manager can manage handlers without referencing their instances.
    /// </summary>
    public interface IEventHandlerContext : IContext
    {
        /// <summary>
        /// Gets the event id.
        /// </summary>
        IComponentId EventId { get; }

        /// <summary>
        /// Gets the event handler id.
        /// </summary>
        string EventHandlerId { get; }

        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        IApplicationContext ApplicationContext { get; }
    }
}
