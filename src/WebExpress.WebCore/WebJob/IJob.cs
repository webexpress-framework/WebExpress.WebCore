using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebJob
{
    /// <summary>
    /// A task that can be performed cyclically.
    /// </summary>
    public interface IJob : IComponent, System.IDisposable
    {
        /// <summary>
        /// Processing of the job.
        /// </summary>
        public void Process();

    }
}
