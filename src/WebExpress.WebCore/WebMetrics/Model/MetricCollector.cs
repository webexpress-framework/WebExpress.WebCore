using System;
using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebMetrics.Model
{
    /// <summary>
    /// Buffers the values of one source during a scrape, so that a source failing halfway leaves no
    /// partial state behind and its values can be merged with all others afterwards.
    /// </summary>
    internal sealed class MetricCollector : IMetricCollector
    {
        private readonly MetricLabel[] _scope;
        private readonly MetricSet _set = new();
        private readonly List<MetricEntry> _entries = [];

        /// <summary>
        /// Gets the accepted values in the order they were reported.
        /// </summary>
        internal IReadOnlyList<MetricEntry> Entries => _entries;

        /// <summary>
        /// Creates a collector whose series all carry the given labels, such as the application of a component.
        /// </summary>
        /// <param name="scope">The labels added to every series; their names are reserved for the source.</param>
        internal MetricCollector(params MetricLabel[] scope)
        {
            _scope = scope ?? [];
        }

        /// <summary>
        /// Reports a count the source keeps itself.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="value">The non-negative, finite count.</param>
        /// <param name="labels">The labels distinguishing the series.</param>
        /// <exception cref="ArgumentException">The value or a name is invalid, or the series contradicts an earlier one.</exception>
        public void Counter(string name, string help, double value, params MetricLabel[] labels)
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentException($"The counter '{name}' must be finite and non-negative.", nameof(value));
            }

            Add(name, help, MetricType.Counter, new MetricSample(name, Scope(labels), value));
        }

        /// <summary>
        /// Reports a value read at scrape time.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="help">The description shown by monitoring tools.</param>
        /// <param name="value">The current value.</param>
        /// <param name="labels">The labels distinguishing the series.</param>
        /// <exception cref="ArgumentException">A name is invalid, or the series contradicts an earlier one.</exception>
        public void Gauge(string name, string help, double value, params MetricLabel[] labels)
        {
            Add(name, help, MetricType.Gauge, new MetricSample(name, Scope(labels), value));
        }

        /// <summary>
        /// Reports every series of an instrument.
        /// </summary>
        /// <param name="instrument">The instrument to report.</param>
        /// <exception cref="ArgumentException">The instrument declares a reserved label, or a series contradicts an earlier one.</exception>
        public void Add(MetricInstrument instrument)
        {
            ArgumentNullException.ThrowIfNull(instrument);

            if (instrument.LabelNames.Any(x => _scope.Any(y => y.Name == x)))
            {
                throw new ArgumentException($"The metric '{instrument.Name}' declares a label reserved by the framework.", nameof(instrument));
            }

            foreach (var sample in instrument.Collect())
            {
                Add(instrument.Name, instrument.Help, instrument.Type, new MetricSample(sample.Name, [.. _scope, .. sample.Labels], sample.Value));
            }
        }

        /// <summary>
        /// Validates the labels of a reported value and prefixes the labels of the source.
        /// </summary>
        /// <param name="labels">The labels supplied by the source.</param>
        /// <returns>The complete label set of the series.</returns>
        /// <exception cref="ArgumentException">A label is invalid, reserved or repeated.</exception>
        private MetricLabel[] Scope(MetricLabel[] labels)
        {
            labels ??= [];

            foreach (var label in labels)
            {
                MetricText.ValidateLabelName(label.Name);
            }

            MetricLabel[] all = [.. _scope, .. labels];

            if (all.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != all.Length)
            {
                throw new ArgumentException("A label is repeated or reserved by the framework.", nameof(labels));
            }

            return all;
        }

        /// <summary>
        /// Accepts a value unless it would make the scrape invalid.
        /// </summary>
        /// <param name="name">The family name.</param>
        /// <param name="help">The family description.</param>
        /// <param name="type">The family type.</param>
        /// <param name="sample">The sample to add.</param>
        /// <exception cref="ArgumentException">The name is invalid, or the series contradicts an earlier one.</exception>
        private void Add(string name, string help, MetricType type, MetricSample sample)
        {
            MetricText.ValidateMetricName(name);

            if (!_set.TryAdd(name, help, type, sample, out var conflict))
            {
                throw new ArgumentException(conflict);
            }

            _entries.Add(new MetricEntry(name, help ?? string.Empty, type, sample));
        }
    }

    /// <summary>
    /// Keeps a reported value together with the family it belongs to until all sources are merged.
    /// </summary>
    /// <param name="Name">The family name.</param>
    /// <param name="Help">The family description.</param>
    /// <param name="Type">The family type.</param>
    /// <param name="Sample">The reported sample.</param>
    internal readonly record struct MetricEntry(string Name, string Help, MetricType Type, MetricSample Sample);
}
