using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WebExpress.WebCore.WebMetrics.Model
{
    /// <summary>
    /// Holds the naming and escaping rules of the Prometheus text format in one place, since a single
    /// malformed line makes the monitoring system reject the whole scrape, not just that series.
    /// </summary>
    internal static partial class MetricText
    {
        /// <summary>
        /// The media type of the Prometheus text exposition format.
        /// </summary>
        internal const string ContentType = "text/plain; version=0.0.4; charset=utf-8";

        [GeneratedRegex("^[a-zA-Z_:][a-zA-Z0-9_:]*$")]
        private static partial Regex MetricNameRegex();

        [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
        private static partial Regex LabelNameRegex();

        /// <summary>
        /// Rejects a metric name the text format cannot carry.
        /// </summary>
        /// <param name="name">The metric name to check.</param>
        /// <exception cref="ArgumentException">The name is empty or contains invalid characters.</exception>
        internal static void ValidateMetricName(string name)
        {
            if (string.IsNullOrEmpty(name) || !MetricNameRegex().IsMatch(name))
            {
                throw new ArgumentException($"'{name}' is not a valid metric name.", nameof(name));
            }
        }

        /// <summary>
        /// Rejects a label name the text format cannot carry or that Prometheus reserves for itself.
        /// </summary>
        /// <param name="name">The label name to check.</param>
        /// <exception cref="ArgumentException">The name is invalid or reserved.</exception>
        internal static void ValidateLabelName(string name)
        {
            if (string.IsNullOrEmpty(name) || !LabelNameRegex().IsMatch(name) || name.StartsWith("__", StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{name}' is not a valid label name.", nameof(name));
            }
        }

        /// <summary>
        /// Writes a number the way the Prometheus parser reads it, independent of the server culture.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>The invariant text form, using <c>+Inf</c>, <c>-Inf</c> and <c>NaN</c> for special values.</returns>
        internal static string FormatValue(double value)
        {
            return double.IsPositiveInfinity(value) ? "+Inf"
                : double.IsNegativeInfinity(value) ? "-Inf"
                : double.IsNaN(value) ? "NaN"
                : value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Builds the identity of a series, independent of the order its labels were given in, so
        /// that duplicates are detected before they reach the monitoring system.
        /// </summary>
        /// <param name="name">The series name.</param>
        /// <param name="labels">The labels of the series.</param>
        /// <returns>A key that is equal for equal series.</returns>
        internal static string SeriesKey(string name, IEnumerable<MetricLabel> labels)
        {
            return name + FormatLabels(labels.OrderBy(x => x.Name, StringComparer.Ordinal));
        }

        /// <summary>
        /// Renders families in the Prometheus text exposition format.
        /// </summary>
        /// <param name="families">The collected families.</param>
        /// <returns>The exposition text.</returns>
        internal static string Format(IEnumerable<MetricFamily> families)
        {
            var builder = new StringBuilder();

            foreach (var family in families)
            {
                builder.Append("# HELP ").Append(family.Name).Append(' ').Append(EscapeHelp(family.Help)).Append('\n');
                builder.Append("# TYPE ").Append(family.Name).Append(' ').Append(family.Type.ToString().ToLowerInvariant()).Append('\n');

                foreach (var sample in family.Samples)
                {
                    builder.Append(sample.Name).Append(FormatLabels(sample.Labels)).Append(' ')
                        .Append(FormatValue(sample.Value)).Append('\n');
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Renders a label set including its braces, or nothing for a series without labels.
        /// </summary>
        /// <param name="labels">The labels to render.</param>
        /// <returns>The label set in exposition form.</returns>
        private static string FormatLabels(IEnumerable<MetricLabel> labels)
        {
            var rendered = string.Join(",", labels.Select(x => $"{x.Name}=\"{EscapeLabelValue(x.Value)}\""));
            return rendered.Length == 0 ? string.Empty : "{" + rendered + "}";
        }

        /// <summary>
        /// Escapes a label value, which may contain any text an application passes.
        /// </summary>
        /// <param name="value">The raw label value.</param>
        /// <returns>The escaped value.</returns>
        private static string EscapeLabelValue(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        /// <summary>
        /// Escapes a description; unlike a label value it may contain unescaped quotes.
        /// </summary>
        /// <param name="help">The raw description.</param>
        /// <returns>The escaped description.</returns>
        private static string EscapeHelp(string help)
        {
            return (help ?? string.Empty).Replace("\\", "\\\\").Replace("\n", "\\n");
        }
    }
}
