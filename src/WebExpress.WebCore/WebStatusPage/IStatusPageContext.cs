using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebStatusPage
{
    /// <summary>
    /// Read-only descriptor of a registered status page, exposing the application and plugin it
    /// belongs to, so the framework can pick and manage status pages without referencing their instances.
    /// </summary>
    public interface IStatusPageContext : IContext
    {
        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the associated application context.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the status page id.
        /// </summary>
        IComponentId StatusPageId { get; }

        /// <summary>
        /// Gets the status code.
        /// </summary>
        int StatusCode { get; }

        /// <summary>
        /// Gets the status title.
        /// </summary>
        string StatusTitle { get; }

        /// <summary>
        /// Gets the status icon.
        /// </summary>
        IRoute StatusIcon { get; }

        /// <summary>
        /// Gets the description of the current status.
        /// </summary>
        string StatusDescription { get; }
    }
}
