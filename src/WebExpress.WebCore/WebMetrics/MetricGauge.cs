using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Tracks a value that rises and falls, such as open connections or queued jobs, when the value
    /// is known where it changes rather than at scrape time.
    /// </summary>
    public sealed class MetricGauge : MetricInstrument
    {
        private readonly ConcurrentDictionary<string[], Cell> _cells = new(LabelValuesComparer.Instance);

        /// <summary>
        /// Gets the gauge type.
        /// </summary>
        public override MetricType Type => MetricType.Gauge;

        /// <summary>
        /// Declares the gauge.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        public MetricGauge(string name, string help, params string[] labelNames)
            : base(name, help, labelNames)
        {
            if (LabelNames.Count == 0)
            {
                _cells.TryAdd([], new Cell());
            }
        }

        /// <summary>
        /// Replaces the value of a series.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        public void Set(double value, params string[] labelValues)
        {
            Interlocked.Exchange(ref GetCell(_cells, labelValues, () => new Cell()).Value, value);
        }

        /// <summary>
        /// Changes the value of a series by a positive or negative amount.
        /// </summary>
        /// <param name="delta">The amount to add.</param>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        public void Add(double delta, params string[] labelValues)
        {
            Add(ref GetCell(_cells, labelValues, () => new Cell()).Value, delta);
        }

        /// <summary>
        /// Raises the value of a series by one.
        /// </summary>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        public void Increment(params string[] labelValues)
        {
            Add(1, labelValues);
        }

        /// <summary>
        /// Lowers the value of a series by one.
        /// </summary>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        public void Decrement(params string[] labelValues)
        {
            Add(-1, labelValues);
        }

        /// <summary>
        /// Reads the current value of a series.
        /// </summary>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        /// <returns>The value, or zero when nothing has been recorded for the series.</returns>
        public double GetValue(params string[] labelValues)
        {
            return _cells.TryGetValue(CheckLabelValues(labelValues), out var cell) ? Volatile.Read(ref cell.Value) : 0;
        }

        /// <summary>
        /// Reads the current value of every series.
        /// </summary>
        /// <returns>One sample per series.</returns>
        internal override IEnumerable<MetricSample> Collect()
        {
            return _cells.Select(x => new MetricSample(Name, ToLabels(x.Key), Volatile.Read(ref x.Value.Value))).ToArray();
        }

        /// <summary>
        /// Holds the value of one series so that it can be updated in place.
        /// </summary>
        private sealed class Cell
        {
            /// <summary>
            /// The current value.
            /// </summary>
            public double Value;
        }
    }
}
