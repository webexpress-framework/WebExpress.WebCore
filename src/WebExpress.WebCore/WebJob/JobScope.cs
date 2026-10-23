namespace WebExpress.WebCore.WebJob
{
    /// <summary>
    /// Tells where a scheduled job runs when several instances of the server share the work.
    /// </summary>
    public enum JobScope
    {
        /// <summary>
        /// Once per due time for the whole cluster, on whichever instance claims it first. Right
        /// for jobs that act on shared data - sending mail, cleaning a database - which would
        /// otherwise run once per instance.
        /// </summary>
        Cluster,

        /// <summary>
        /// On every instance. Right for jobs that maintain something local to the process, such
        /// as an in-memory cache or a file on the instance's own disk.
        /// </summary>
        Node
    }
}
