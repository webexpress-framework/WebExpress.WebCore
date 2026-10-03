using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebJob
{
    /// <summary>
    /// Default implementation of <see cref="IJobContext"/>: the read-only descriptor that identifies
    /// a scheduled job and the application and plugin it belongs to.
    /// </summary>
    public class JobContext : IJobContext
    {
        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the job id.
        /// </summary>
        public IComponentId JobId { get; internal set; }

        /// <summary>
        /// Gets the name of the job, which may be an internationalization key.
        /// </summary>
        public string JobName { get; internal set; }

        /// <summary>
        /// Gets the description of the job, which may be an internationalization key.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the cron-object.
        /// </summary>
        public Cron Cron { get; internal set; }
    }
}
