using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebStatusPage
{
    /// <summary>
    /// Default implementation of <see cref="IStatusPageContext"/>: the read-only descriptor that
    /// identifies a registered status page and the application and plugin it belongs to.
    /// </summary>
    public class StatusPageContext : IStatusPageContext
    {
        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the associated application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the status id.
        /// </summary>
        public IComponentId StatusPageId { get; internal set; }

        /// <summary>
        /// Gets the status code.
        /// </summary>
        public int StatusCode { get; internal set; }

        /// <summary>
        /// Gets the status title.
        /// </summary>
        public string StatusTitle { get; internal set; }

        /// <summary>
        /// Gets the status icon.
        /// </summary>
        public IRoute StatusIcon { get; internal set; }

        /// <summary>
        /// Gets the description of the current status.
        /// </summary>
        public string StatusDescription { get; internal set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"StatusPage: {StatusPageId}";
        }
    }
}
