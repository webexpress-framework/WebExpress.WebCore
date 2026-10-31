using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebMetrics.Model;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Records values where they occur - a login, a database call, an LDAP request - so that a scrape
    /// only reads them. Instruments are safe to update from any number of request threads at once.
    /// </summary>
    /// <remarks>
    /// An instrument reaches the metrics endpoint in one of two ways: it is created through
    /// <see cref="IMetricsManager"/>, which exports it on every scrape, or an <see cref="IMetric"/>
    /// component hands it to its collector, which adds the application label.
    /// </remarks>
    public abstract class MetricInstrument
    {
        /// <summary>
        /// Gets the metric name, matching <c>[a-zA-Z_:][a-zA-Z0-9_:]*</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description shown by monitoring tools.
        /// </summary>
        public string Help { get; }

        /// <summary>
        /// Gets how the series of the instrument may be aggregated.
        /// </summary>
        public abstract MetricType Type { get; }

        /// <summary>
        /// Gets the names of the labels every update must supply values for, in order.
        /// </summary>
        public IReadOnlyList<string> LabelNames { get; }

        /// <summary>
        /// Validates the declaration once, so that an invalid name fails where the instrument is
        /// defined instead of breaking every later scrape.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <exception cref="ArgumentException">The name or a label name is invalid or repeated.</exception>
        private protected MetricInstrument(string name, string help, string[] labelNames)
        {
            MetricText.ValidateMetricName(name);
            labelNames ??= [];

            foreach (var labelName in labelNames)
            {
                MetricText.ValidateLabelName(labelName);
            }

            if (labelNames.Distinct(StringComparer.Ordinal).Count() != labelNames.Length)
            {
                throw new ArgumentException($"The metric '{name}' declares a label more than once.", nameof(labelNames));
            }

            Name = name;
            Help = help ?? string.Empty;
            LabelNames = labelNames.ToArray();
        }

        /// <summary>
        /// Reads the current value of every series.
        /// </summary>
        /// <returns>The samples, labelled with the instrument's own labels only.</returns>
        internal abstract IEnumerable<MetricSample> Collect();

        /// <summary>
        /// Finds the storage of a series, creating it on first use, so that series appear only once
        /// something has been recorded for them.
        /// </summary>
        /// <typeparam name="TCell">The storage type of one series.</typeparam>
        /// <param name="cells">The storage of all series of the instrument.</param>
        /// <param name="labelValues">The label values identifying the series.</param>
        /// <param name="create">Creates the storage of a new series.</param>
        /// <returns>The storage of the series.</returns>
        private protected TCell GetCell<TCell>(ConcurrentDictionary<string[], TCell> cells, string[] labelValues, Func<TCell> create)
        {
            var values = CheckLabelValues(labelValues);

            // the caller may reuse its array, so only a private copy is safe as a key
            return cells.TryGetValue(values, out var cell) ? cell : cells.GetOrAdd((string[])values.Clone(), _ => create());
        }

        /// <summary>
        /// Rejects label values that do not match the declaration, since a series without one of
        /// its labels cannot be told apart from another.
        /// </summary>
        /// <param name="labelValues">The label values supplied by the caller.</param>
        /// <returns>The validated values.</returns>
        /// <exception cref="ArgumentException">The number of values differs from the declared labels, or a value is null.</exception>
        private protected string[] CheckLabelValues(string[] labelValues)
        {
            labelValues ??= [];

            if (labelValues.Length != LabelNames.Count || labelValues.Any(x => x is null))
            {
                throw new ArgumentException($"The metric '{Name}' expects values for the labels [{string.Join(", ", LabelNames)}].",
                    nameof(labelValues));
            }

            return labelValues;
        }

        /// <summary>
        /// Pairs stored label values with their declared names.
        /// </summary>
        /// <param name="labelValues">The values of a series.</param>
        /// <returns>The labels of the series.</returns>
        private protected MetricLabel[] ToLabels(string[] labelValues)
        {
            return LabelNames.Select((x, i) => new MetricLabel(x, labelValues[i])).ToArray();
        }

        /// <summary>
        /// Adds to a shared value without a lock, so that request threads never wait for each other.
        /// </summary>
        /// <param name="target">The value to change.</param>
        /// <param name="amount">The amount to add.</param>
        private protected static void Add(ref double target, double amount)
        {
            double initial;
            double computed;

            // compared by bits: a NaN never equals itself and would otherwise retry forever
            do
            {
                initial = Volatile.Read(ref target);
                computed = initial + amount;
            }
            while (BitConverter.DoubleToInt64Bits(Interlocked.CompareExchange(ref target, computed, initial))
                != BitConverter.DoubleToInt64Bits(initial));
        }

        /// <summary>
        /// Compares label value arrays by content, since every update passes a new array.
        /// </summary>
        private protected sealed class LabelValuesComparer : IEqualityComparer<string[]>
        {
            /// <summary>
            /// Gets the shared stateless instance.
            /// </summary>
            public static LabelValuesComparer Instance { get; } = new();

            /// <summary>
            /// Treats arrays with the same values in the same order as the same series.
            /// </summary>
            /// <param name="x">The first array.</param>
            /// <param name="y">The second array.</param>
            /// <returns>True when both identify the same series.</returns>
            public bool Equals(string[] x, string[] y)
            {
                return ReferenceEquals(x, y) || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));
            }

            /// <summary>
            /// Combines the hash codes of all values.
            /// </summary>
            /// <param name="obj">The array.</param>
            /// <returns>The combined hash code.</returns>
            public int GetHashCode(string[] obj)
            {
                var hash = new HashCode();

                foreach (var value in obj)
                {
                    hash.Add(value, StringComparer.Ordinal);
                }

                return hash.ToHashCode();
            }
        }
    }
}
