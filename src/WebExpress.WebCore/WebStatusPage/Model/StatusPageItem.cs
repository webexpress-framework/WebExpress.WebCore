using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebStatusPage.Model
{
    /// <summary>
    /// Internal record the status-page manager keeps for one registered status page, linking it to
    /// the application and plugin that provided it.
    /// </summary>
    internal class StatusPageItem
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
        /// Gets status page context.
        /// </summary>
        public StatusPageContext StatusPageContext { get; internal set; }

        /// <summary>
        /// Gets the status code.
        /// </summary>
        public Type StatusResponse { get; internal set; }

        /// <summary>
        /// Gets the type of status page.
        /// </summary>
        public Type StatusPageClass { get; internal set; }

        /// <summary>
        /// Performs application-specific tasks related to sharing, returning, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Convert the status page element to a string.
        /// </summary>
        /// <returns>The status page element in its string representation.</returns>
        public override string ToString()
        {
            return $"StatusPage '{StatusPageContext?.StatusPageId}'";
        }
    }
}
