namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Tells a monitoring system how a series may be aggregated, since a rate over a gauge or a
    /// sum over a counter yields numbers that look valid but mean nothing.
    /// </summary>
    public enum MetricType
    {
        /// <summary>
        /// A value that only grows while the process lives, such as handled requests or failed logins.
        /// </summary>
        Counter,

        /// <summary>
        /// A value that rises and falls, such as memory in use or active users.
        /// </summary>
        Gauge,

        /// <summary>
        /// A distribution of observations in cumulative buckets, such as response times.
        /// </summary>
        Histogram
    }
}
