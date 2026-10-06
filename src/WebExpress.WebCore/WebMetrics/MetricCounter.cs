using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Counts events such as logins, failed requests or LDAP queries. The value only ever grows, which
    /// lets the monitoring system derive rates and detect a process restart from a drop to zero.
    /// </summary>
    public sealed class MetricCounter : MetricInstrument
    {
        private readonly ConcurrentDictionary<string[], Cell> _cells = new(LabelValuesComparer.Instance);

        /// <summary>
        /// Gets the counter type.
        /// </summary>
        public override MetricType Type => MetricType.Counter;

        /// <summary>
        /// Declares the counter. By convention its name ends in <c>_total</c>.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        public MetricCounter(string name, string help, params string[] labelNames)
            : base(name, help, labelNames)
        {
            // a counter without labels is reported as zero from the start, so that a rate is
            // defined before the first event and an alert on its absence does not fire
            if (LabelNames.Count == 0)
            {
                _cells.TryAdd([], new Cell());
            }
        }

        /// <summary>
        /// Counts one event.
        /// </summary>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        public void Increment(params string[] labelValues)
        {
            Add(1, labelValues);
        }

        /// <summary>
        /// Counts several events or a quantity such as transferred bytes.
        /// </summary>
        /// <param name="amount">The non-negative, finite amount to add.</param>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        /// <exception cref="ArgumentOutOfRangeException">The amount is negative, infinite or not a number.</exception>
        public void Add(double amount, params string[] labelValues)
        {
            if (!double.IsFinite(amount) || amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "A counter can only grow by a finite, non-negative amount.");
            }

            Add(ref GetCell(_cells, labelValues, () => new Cell()).Value, amount);
        }

        /// <summary>
        /// Reads the current value of a series.
        /// </summary>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        /// <returns>The value, or zero when nothing has been counted for the series.</returns>
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
