using WebExpress.WebCore.WebJob;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Declares where a job runs when several instances of the server form a cluster. Without
    /// the attribute a job runs once per due time for the whole cluster, since most jobs act on
    /// shared data and running them on every instance would repeat their effect.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class JobScopeAttribute : System.Attribute, IJobAttribute
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="scope">Where the job runs.</param>
        public JobScopeAttribute(JobScope scope)
        {
        }
    }
}
