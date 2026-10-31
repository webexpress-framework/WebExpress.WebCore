using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Records a distribution such as response or query times. Unlike an average, the buckets let the
    /// monitoring system compute percentiles across all instances, which is what latency alerts need.
    /// </summary>
    public sealed class MetricHistogram : MetricInstrument
    {
        private readonly ConcurrentDictionary<string[], Cell> _cells = new(LabelValuesComparer.Instance);
        private readonly double[] _buckets;

        /// <summary>
        /// Gets the bucket boundaries suited to durations in seconds, from five milliseconds to ten seconds.
        /// </summary>
        public static IReadOnlyList<double> DefaultBuckets { get; } = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

        /// <summary>
        /// Gets the histogram type.
        /// </summary>
        public override MetricType Type => MetricType.Histogram;

        /// <summary>
        /// Gets the upper bounds of the buckets; observations above the last bound are counted in an
        /// implicit <c>+Inf</c> bucket.
        /// </summary>
        public IReadOnlyList<double> Buckets => _buckets;

        /// <summary>
        /// Declares the histogram. The buckets are fixed for its lifetime because changing them would
        /// make earlier and later scrapes incomparable.
        /// </summary>
        /// <param name="name">The metric name, by convention ending in the unit, such as <c>_seconds</c>.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="buckets">The finite, strictly ascending upper bounds, or null for <see cref="DefaultBuckets"/>.</param>
        /// <param name="labelNames">The names of the labels distinguishing the series.</param>
        /// <exception cref="ArgumentException">The buckets are empty, not ascending or not finite, or a label is named <c>le</c>.</exception>
        public MetricHistogram(string name, string help, IEnumerable<double> buckets, params string[] labelNames)
            : base(name, help, labelNames)
        {
            if (LabelNames.Contains("le"))
            {
                throw new ArgumentException("The label 'le' is reserved for the bucket bounds of a histogram.", nameof(labelNames));
            }

            _buckets = (buckets ?? DefaultBuckets).ToArray();

            if (_buckets.Length == 0 || _buckets.Any(x => !double.IsFinite(x)) ||
                _buckets.Zip(_buckets.Skip(1)).Any(x => x.First >= x.Second))
            {
                throw new ArgumentException("The buckets must be finite and strictly ascending.", nameof(buckets));
            }

            if (LabelNames.Count == 0)
            {
                _cells.TryAdd([], new Cell(_buckets.Length));
            }
        }

        /// <summary>
        /// Records one observation.
        /// </summary>
        /// <param name="value">The observed value, such as a duration in seconds.</param>
        /// <param name="labelValues">The label values of the series, in declaration order.</param>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a number.</exception>
        public void Observe(double value, params string[] labelValues)
        {
            if (double.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A histogram cannot record a value that is not a number.");
            }

            var cell = GetCell(_cells, labelValues, () => new Cell(_buckets.Length));
            var index = Array.BinarySearch(_buckets, value);

            // a bound is inclusive, and a miss yields the complement of the next larger bound
            index = index >= 0 ? index : ~index;

            lock (cell)
            {
                cell.Counts[index]++;
                cell.Sum += value;
            }
        }

        /// <summary>
        /// Reads every series as cumulative buckets, sum and count.
        /// </summary>
        /// <returns>The bucket, sum and count samples of every series.</returns>
        internal override IEnumerable<MetricSample> Collect()
        {
            var samples = new List<MetricSample>();

            foreach (var (labelValues, cell) in _cells)
            {
                long[] counts;
                double sum;

                // copied under the lock so that the count always equals the +Inf bucket
                lock (cell)
                {
                    counts = (long[])cell.Counts.Clone();
                    sum = cell.Sum;
                }

                var labels = ToLabels(labelValues);
                long cumulative = 0;

                for (var i = 0; i < counts.Length; i++)
                {
                    cumulative += counts[i];
                    var bound = i < _buckets.Length ? _buckets[i] : double.PositiveInfinity;
                    samples.Add(new MetricSample(Name + "_bucket",
                        [.. labels, new MetricLabel("le", Model.MetricText.FormatValue(bound))], cumulative));
                }

                samples.Add(new MetricSample(Name + "_sum", labels, sum));
                samples.Add(new MetricSample(Name + "_count", labels, cumulative));
            }

            return samples;
        }

        /// <summary>
        /// Holds the bucket counts and sum of one series, guarded together by locking the cell.
        /// </summary>
        /// <param name="bounds">The number of finite bucket bounds.</param>
        private sealed class Cell(int bounds)
        {
            /// <summary>
            /// The non-cumulative count per bucket, with the +Inf bucket last.
            /// </summary>
            public long[] Counts { get; } = new long[bounds + 1];

            /// <summary>
            /// The sum of all observations.
            /// </summary>
            public double Sum { get; set; }
        }
    }
}
