using WebExpress.WebCore.WebMetrics;
using WebExpress.WebCore.WebMetrics.Model;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Verifies that instruments keep their semantics under concurrency and only ever produce
    /// exposition text Prometheus accepts.
    /// </summary>
    public class UnitTestMetricInstruments
    {
        /// <summary>
        /// Keeps separate series per label values and loses no increment under concurrent updates.
        /// </summary>
        [Fact]
        public void Counter_ConcurrentIncrements_AreNotLost()
        {
            // arrange
            var counter = new MetricCounter("logins_total", "Logins.", "result");

            // act
            Parallel.For(0, 10000, i => counter.Increment(i % 2 == 0 ? "success" : "failure"));
            counter.Add(0.5, "success");

            // validation
            Assert.Equal(5000.5, counter.GetValue("success"));
            Assert.Equal(5000, counter.GetValue("failure"));
            Assert.Equal(0, counter.GetValue("throttled"));
        }

        /// <summary>
        /// Refuses updates that would break the counter contract or address a series ambiguously.
        /// </summary>
        [Fact]
        public void Counter_InvalidUpdates_AreRejected()
        {
            // arrange
            var counter = new MetricCounter("events_total", "Events.", "kind");

            // validation
            Assert.Throws<ArgumentOutOfRangeException>(() => counter.Add(-1, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => counter.Add(double.NaN, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => counter.Add(double.PositiveInfinity, "a"));
            Assert.Throws<ArgumentException>(() => counter.Increment());
            Assert.Throws<ArgumentException>(() => counter.Increment("a", "b"));
            Assert.Throws<ArgumentException>(() => counter.Increment([null]));
        }

        /// <summary>
        /// Rejects declarations whose names or labels cannot be carried by the text format.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="labels">The comma separated label names.</param>
        [Theory]
        [InlineData("", "")]
        [InlineData("1metric", "")]
        [InlineData("metric-name", "")]
        [InlineData("metric", "__reserved")]
        [InlineData("metric", "bad-label")]
        [InlineData("metric", "a,a")]
        public void Declaration_InvalidNames_AreRejected(string name, string labels)
        {
            // arrange
            string[] labelNames = labels.Length == 0 ? [] : labels.Split(',');

            // validation
            Assert.Throws<ArgumentException>(() => new MetricGauge(name, "", labelNames));
        }

        /// <summary>
        /// Supports both absolute and relative updates of a gauge.
        /// </summary>
        [Fact]
        public void Gauge_SetAndAdjust()
        {
            // arrange
            var gauge = new MetricGauge("queue_length", "Queued jobs.");

            // act
            gauge.Set(10);
            gauge.Increment();
            gauge.Decrement();
            gauge.Decrement();
            gauge.Add(-2.5);

            // validation
            Assert.Equal(6.5, gauge.GetValue());
        }

        /// <summary>
        /// Places observations in inclusive buckets and reports them cumulatively, with the count
        /// always equal to the +Inf bucket.
        /// </summary>
        [Fact]
        public void Histogram_ReportsCumulativeBuckets()
        {
            // arrange
            var histogram = new MetricHistogram("query_duration_seconds", "Query time.", [0.125, 1], "db");

            // act
            histogram.Observe(0.0625, "main");
            histogram.Observe(0.125, "main");
            histogram.Observe(0.5, "main");
            histogram.Observe(3, "main");
            var text = Format(histogram);

            // validation
            Assert.Contains("query_duration_seconds_bucket{db=\"main\",le=\"0.125\"} 2\n", text);
            Assert.Contains("query_duration_seconds_bucket{db=\"main\",le=\"1\"} 3\n", text);
            Assert.Contains("query_duration_seconds_bucket{db=\"main\",le=\"+Inf\"} 4\n", text);
            Assert.Contains("query_duration_seconds_sum{db=\"main\"} 3.6875\n", text);
            Assert.Contains("query_duration_seconds_count{db=\"main\"} 4\n", text);
            Assert.Contains("# TYPE query_duration_seconds histogram\n", text);
        }

        /// <summary>
        /// Rejects bucket layouts and labels that would produce an invalid histogram.
        /// </summary>
        [Fact]
        public void Histogram_InvalidDeclaration_IsRejected()
        {
            // validation
            Assert.Throws<ArgumentException>(() => new MetricHistogram("h", "", [1, 1]));
            Assert.Throws<ArgumentException>(() => new MetricHistogram("h", "", [2, 1]));
            Assert.Throws<ArgumentException>(() => new MetricHistogram("h", "", []));
            Assert.Throws<ArgumentException>(() => new MetricHistogram("h", "", [double.PositiveInfinity]));
            Assert.Throws<ArgumentException>(() => new MetricHistogram("h", "", null, "le"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetricHistogram("h", "", null).Observe(double.NaN));
        }

        /// <summary>
        /// Escapes label values and descriptions and writes numbers independent of the server culture.
        /// </summary>
        [Fact]
        public void Format_EscapesTextAndUsesInvariantNumbers()
        {
            // arrange
            var previous = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var gauge = new MetricGauge("temperature_celsius", "Line one\nback\\slash \"quoted\"", "sensor");
            gauge.Set(21.5, "a \"b\"\\c\nd");
            gauge.Set(double.NaN, "nan");
            gauge.Set(double.NegativeInfinity, "low");

            try
            {
                // act
                var text = Format(gauge);

                // validation
                Assert.Contains("# HELP temperature_celsius Line one\\nback\\\\slash \"quoted\"\n", text);
                Assert.Contains("temperature_celsius{sensor=\"a \\\"b\\\"\\\\c\\nd\"} 21.5\n", text);
                Assert.Contains("temperature_celsius{sensor=\"nan\"} NaN\n", text);
                Assert.Contains("temperature_celsius{sensor=\"low\"} -Inf\n", text);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }

        /// <summary>
        /// Renders a single instrument as a scrape would.
        /// </summary>
        /// <param name="instrument">The instrument to render.</param>
        /// <returns>The exposition text.</returns>
        private static string Format(MetricInstrument instrument)
        {
            var collector = new MetricCollector();
            collector.Add(instrument);
            var set = new MetricSet();
            foreach (var entry in collector.Entries)
            {
                set.TryAdd(entry.Name, entry.Help, entry.Type, entry.Sample, out _);
            }

            return MetricText.Format(set.ToFamilies());
        }
    }
}
