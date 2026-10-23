using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebMetrics.Model
{
    /// <summary>
    /// Merges contributions into families while keeping the output acceptable to Prometheus, which
    /// rejects an entire scrape when a name changes its type or a series appears twice.
    /// </summary>
    internal sealed class MetricSet
    {
        private readonly List<Entry> _families = [];
        private readonly Dictionary<string, Entry> _byName = [];
        private readonly HashSet<string> _series = [];

        /// <summary>
        /// Adds a sample unless it contradicts what the set already holds.
        /// </summary>
        /// <param name="name">The family name.</param>
        /// <param name="help">The family description; the first contribution wins.</param>
        /// <param name="type">The family type.</param>
        /// <param name="sample">The sample to add.</param>
        /// <param name="conflict">The reason the sample was refused, or null.</param>
        /// <returns>True when the sample was added.</returns>
        internal bool TryAdd(string name, string help, MetricType type, MetricSample sample, out string conflict)
        {
            if (_byName.TryGetValue(name, out var entry) && entry.Type != type)
            {
                conflict = $"The metric '{name}' is already declared as {entry.Type.ToString().ToLowerInvariant()}" +
                    $" and cannot be reported as {type.ToString().ToLowerInvariant()}.";
                return false;
            }

            if (!_series.Add(MetricText.SeriesKey(sample.Name, sample.Labels)))
            {
                conflict = $"The series '{MetricText.SeriesKey(sample.Name, sample.Labels)}' is reported more than once.";
                return false;
            }

            if (entry is null)
            {
                entry = new Entry(name, help, type);
                _byName.Add(name, entry);
                _families.Add(entry);
            }

            entry.Samples.Add(sample);
            conflict = null;
            return true;
        }

        /// <summary>
        /// Freezes the merged state, leaving out families that ended up without a series.
        /// </summary>
        /// <returns>The families in the order they were first contributed.</returns>
        internal IReadOnlyList<MetricFamily> ToFamilies()
        {
            return _families.Where(x => x.Samples.Count > 0)
                .Select(x => new MetricFamily(x.Name, x.Help, x.Type, x.Samples.ToArray()))
                .ToArray();
        }

        /// <summary>
        /// Accumulates the samples of one family while the set is still being filled.
        /// </summary>
        /// <param name="Name">The family name.</param>
        /// <param name="Help">The family description.</param>
        /// <param name="Type">The family type.</param>
        private sealed record Entry(string Name, string Help, MetricType Type)
        {
            /// <summary>
            /// Gets the samples added so far.
            /// </summary>
            public List<MetricSample> Samples { get; } = [];
        }
    }
}
