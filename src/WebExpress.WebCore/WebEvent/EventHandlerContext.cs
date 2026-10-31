using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebEvent
{
    /// <summary>
    /// Default implementation of <see cref="IEventHandlerContext"/>: the read-only descriptor that
    /// identifies a registered event handler and the application and plugin it belongs to.
    /// </summary>
    public class EventHandlerContext : IEventHandlerContext
    {
        /// <summary>
        /// Gets the event id.
        /// </summary>
        public IComponentId EventId { get; internal set; }

        /// <summary>
        /// Gets the event handler id.
        /// </summary>
        public string EventHandlerId { get; internal set; }

        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"Event: {EventId}";
        }
    }
}
