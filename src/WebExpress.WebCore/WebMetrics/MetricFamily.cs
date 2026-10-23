using System.Collections.Generic;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Groups every series of one metric under a single description and type, which is the unit a
    /// monitoring system reads and the reason one name can never be both a counter and a gauge.
    /// </summary>
    public sealed class MetricFamily
    {
        /// <summary>
        /// Gets the metric name shared by all series of the family.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description shown by monitoring tools.
        /// </summary>
        public string Help { get; }

        /// <summary>
        /// Gets how the series of the family may be aggregated.
        /// </summary>
        public MetricType Type { get; }

        /// <summary>
        /// Gets the collected series in the order they were contributed.
        /// </summary>
        public IReadOnlyList<MetricSample> Samples { get; }

        /// <summary>
        /// Creates an immutable family once all contributions of a collection have been merged.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="type">How the series may be aggregated.</param>
        /// <param name="samples">The collected series.</param>
        internal MetricFamily(string name, string help, MetricType type, IReadOnlyList<MetricSample> samples)
        {
            Name = name;
            Help = help;
            Type = type;
            Samples = samples;
        }
    }
}
