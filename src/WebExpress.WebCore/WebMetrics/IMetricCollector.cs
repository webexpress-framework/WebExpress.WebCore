using System;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Receives the values an <see cref="IMetric"/> component reports during a scrape. Every series is
    /// labelled with the application the component is bound to, so the same component in several
    /// applications yields distinct series instead of duplicates.
    /// </summary>
    /// <remarks>
    /// The label <c>application</c> is reserved for this purpose. A rejected value - an invalid name,
    /// a reserved label, a name reported with two types, or a series reported twice - throws an
    /// <see cref="ArgumentException"/>, and none of the values of the component reach that scrape.
    /// </remarks>
    public interface IMetricCollector
    {
        /// <summary>
        /// Reports a count the component keeps itself, such as the requests counted by a client library.
        /// </summary>
        /// <param name="name">The metric name, by convention ending in <c>_total</c>.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="value">The non-negative, finite count since the process started.</param>
        /// <param name="labels">The labels distinguishing the series.</param>
        void Counter(string name, string help, double value, params MetricLabel[] labels);

        /// <summary>
        /// Reports a value read at scrape time, such as the connections a pool holds open.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="value">The current value.</param>
        /// <param name="labels">The labels distinguishing the series.</param>
        void Gauge(string name, string help, double value, params MetricLabel[] labels);

        /// <summary>
        /// Reports every series an instrument has recorded since it was created.
        /// </summary>
        /// <param name="instrument">The instrument to report.</param>
        void Add(MetricInstrument instrument);
    }
}
