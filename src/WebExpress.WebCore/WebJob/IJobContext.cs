using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebJob
{
    /// <summary>
    /// Read-only descriptor of a registered job (a scheduled, recurring task), exposing the
    /// application and plugin it belongs to, so the job manager can manage it without referencing
    /// the job instance.
    /// </summary>
    public interface IJobContext : IContext
    {
        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the job id.
        /// </summary>
        IComponentId JobId { get; }

        /// <summary>
        /// Gets the name of the job, which may be an internationalization key. Null when the
        /// job declares none, in which case only the id identifies it.
        /// </summary>
        string JobName { get; }

        /// <summary>
        /// Gets the description of the job, which may be an internationalization key. It states
        /// what the job does, because a schedule alone does not say why it runs.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the cron-object.
        /// </summary>
        Cron Cron { get; }
    }
}
