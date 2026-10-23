using System.Collections.Generic;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Carries one value of a series at collection time. A histogram contributes several samples
    /// to its family, which is why the sample keeps its own name.
    /// </summary>
    public sealed class MetricSample
    {
        /// <summary>
        /// Gets the series name, which equals the family name except for the bucket, sum and
        /// count series of a histogram.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the labels identifying the series within its family.
        /// </summary>
        public IReadOnlyList<MetricLabel> Labels { get; }

        /// <summary>
        /// Gets the value read at collection time.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Creates an immutable sample so a collected snapshot cannot change while it is rendered.
        /// </summary>
        /// <param name="name">The series name.</param>
        /// <param name="labels">The labels identifying the series.</param>
        /// <param name="value">The value read at collection time.</param>
        internal MetricSample(string name, IReadOnlyList<MetricLabel> labels, double value)
        {
            Name = name;
            Labels = labels;
            Value = value;
        }
    }
}
