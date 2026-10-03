using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebExpress.WebCore.WebSettingPage.Model
{

    /// <summary>
    /// Converts a TimeSpan object to a formatted string and vice versa.
    /// </summary>
    public partial class TimeSpanConverter
    {
        /// <summary>
        /// Matches the string <see cref="Convert"/> produces. Every unit is optional so a
        /// caller may hand back a shortened form, and each carries its own sign because a
        /// negative span is formatted with the sign on every component.
        /// </summary>
        [GeneratedRegex(@"^\s*(?:(?<d>-?\d+)d)?\s*(?:(?<h>-?\d+)h)?\s*(?:(?<m>-?\d+)m(?!s))?\s*(?:(?<s>-?\d+)s)?\s*(?:(?<ms>-?\d+)ms)?\s*$", RegexOptions.CultureInvariant)]
        private static partial Regex FormattedPattern();

        /// <summary>
        /// Converts a TimeSpan object to a formatted string.
        /// </summary>
        /// <param name="value">The TimeSpan object to convert.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">Optional parameter for conversion.</param>
        /// <param name="language">The language to use in the converter.</param>
        /// <returns>A formatted string representing the TimeSpan.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is null)
            {
                return null;
            }

            var ts = TimeSpan.Parse(value.ToString());
            return string.Format
                (
                    "{0}d {1:D2}h {2:D2}m {3:D2}s {4:D2}ms",
                    ts.Days,
                    ts.Hours,
                    ts.Minutes,
                    ts.Seconds,
                    ts.Milliseconds
                );
        }

        /// <summary>
        /// Converts a formatted string back to a TimeSpan object.
        /// </summary>
        /// <remarks>
        /// The string <see cref="Convert"/> produces is read back component by component;
        /// a plain time span literal is accepted as well, so a value that was never formatted
        /// - one stored as <c>1.02:03:04</c> - round-trips through the same converter.
        /// </remarks>
        /// <param name="value">The formatted string to convert.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">Optional parameter for conversion.</param>
        /// <param name="language">The language to use in the converter.</param>
        /// <returns>The TimeSpan object, or null for a null value.</returns>
        /// <exception cref="FormatException">Thrown when the string is neither the formatted
        /// form nor a time span literal.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is null)
            {
                return null;
            }

            if (value is TimeSpan span)
            {
                return span;
            }

            var text = value.ToString();
            var match = FormattedPattern().Match(text);

            // the pattern is all-optional, so the empty string matches it too and has to be
            // handed on to the literal parser, which rejects it with the right message
            if (match.Success && match.Groups.Values.Skip(1).Any(x => x.Success))
            {
                return new TimeSpan
                (
                    Component(match, "d"),
                    Component(match, "h"),
                    Component(match, "m"),
                    Component(match, "s"),
                    Component(match, "ms")
                );
            }

            return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads one unit of the formatted form.
        /// </summary>
        /// <param name="match">The match of the formatted pattern.</param>
        /// <param name="group">The unit's group name.</param>
        /// <returns>The unit's value, or zero when the unit is not present.</returns>
        private static int Component(Match match, string group)
        {
            var value = match.Groups[group];

            return value.Success ? int.Parse(value.Value, CultureInfo.InvariantCulture) : 0;
        }
    }
}
