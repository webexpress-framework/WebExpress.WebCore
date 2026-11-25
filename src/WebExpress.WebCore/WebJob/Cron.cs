using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;

namespace WebExpress.WebCore.WebJob
{
    /// <summary>
    /// Manages commands that are executed on a scheduled basis (CRON = command run on notice). 
    /// </summary>
    /// <remarks>
    /// For more information, see <a href="https://en.wikipedia.org/wiki/Cron">Cron</a>.
    /// </remarks>
    public class Cron
    {
        /// <summary>
        /// Gets or sets the reference to the context of the host
        /// </summary>
        private static IHttpServerContext Context { get; set; }

        /// <summary>
        /// Gets the minute 0-59 or * for any. Comma seperated values or ranges (-) are also possible.
        /// </summary>
        public IEnumerable<int> Minute { get; private set; }

        /// <summary>
        /// Gets the hour 0-23 or * for any. Comma seperated values or ranges (-) are also possible.
        /// </summary>
        public IEnumerable<int> Hour { get; private set; }

        /// <summary>
        /// Gets the day 1-31 or * for any. Comma seperated values or ranges (-) are also possible.
        /// </summary>
        public IEnumerable<int> Day { get; private set; }

        /// <summary>
        /// Gets the month 1-12 or * for any. Comma seperated values or ranges (-) are also possible.
        /// </summary>
        public IEnumerable<int> Month { get; private set; }

        /// <summary>
        /// Gets the day of the week 0-6 (Sunday-Saturday) or * for any. Comma seperated values or ranges (-) are also possible.
        /// </summary>
        public IEnumerable<int> Weekday { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="context">The reference to the host's context.</param>
        /// <param name="minute">The minute 0-59 or * for any. Comma seperated values or ranges (-) are also possible.</param>
        /// <param name="hour">The hour 0-23 or * for any. Comma seperated values or ranges (-) are also possible.</param>
        /// <param name="day">The day 1-31 or * for any. Comma seperated values or ranges (-) are also possible.</param>
        /// <param name="month">The month 1-12 or * for any. Comma seperated values or ranges (-) are also possible.</param>
        /// <param name="weekday">The day of the week 0-6 (Sunday-Saturday) or * for any. Comma seperated values or ranges (-) are also possible.</param>
        public Cron(IHttpServerContext context, string minute = "*", string hour = "*", string day = "*", string month = "*", string weekday = "*")
        {
            Context = context;

            Minute = Parse(minute, 0, 60);
            Hour = Parse(hour, 0, 24);
            Day = Parse(day, 1, 31);
            Month = Parse(month, 1, 12);
            Weekday = Parse(weekday, 0, 7);
        }

        /// <summary>
        /// Parses the value.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <param name="minValue">The minimum.</param>
        /// <param name="maxValue">The maximum.</param>
        /// <returns>The parsed values.</returns>
        private static IEnumerable<int> Parse(string value, int minValue, int maxValue)
        {
            var items = new List<int>() as IEnumerable<int>;
            value = value?.ToLowerInvariant().Trim();

            if (string.IsNullOrEmpty(value) || value.Equals("*"))
            {
                return Enumerable.Range(minValue, maxValue).ToList();
            }

            var commaSeparatedList = value.Split
            (
                ',',
                System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries
            );

            foreach (var i in commaSeparatedList)
            {
                var range = i.Split('-', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                if (range.Length == 2)
                {
                    // range
                    var min = int.MinValue;
                    var max = int.MinValue;

                    if (int.TryParse(range[0], out int minResult))
                    {
                        min = Math.Max(minResult, minValue);
                    }

                    if (int.TryParse(range[1], out int maxResult))
                    {
                        max = Math.Min(maxResult, maxValue);
                    }

                    if (min != int.MinValue && max != int.MinValue && min < max)
                    {
                        items = items.Union(Enumerable.Range(min, (max - min) + 1));
                    }
                    else
                    {
                        Context.Log?.Warning(message: I18N.Translate("webexpress.webcore:schedulermanager.cron.range"), args: value);
                    }
                }
                else if (range.Length == 1)
                {
                    if (int.TryParse(range[0], out int result))
                    {
                        if (result >= minValue && result <= maxValue)
                        {
                            items = items.Union([result]);
                        }
                        else
                        {
                            Context.Log?.Warning(message: I18N.Translate("webexpress.webcore:schedulermanager.cron.range"), args: result);
                        }
                    }
                    else
                    {
                        Context.Log?.Warning(message: I18N.Translate("webexpress.webcore:schedulermanager.cron.parseerror"), args: value);
                    }
                }
                else
                {
                    Context.Log?.Warning(message: I18N.Translate("webexpress.webcore:schedulermanager.cron.parseerror"), args: value);
                }
            }

            return items;
        }

        /// <summary>
        /// Checks whether the cyclic processing of a command can take place.
        /// </summary>
        /// <returns>True if there is a match, false otherwise.</returns>
        public bool Matching(Clock clock)
        {
            return Minute.Contains(clock.Minute) &&
                   Hour.Contains(clock.Hour) &&
                   Day.Contains(clock.Day) &&
                   Month.Contains(clock.Month) &&
                   Weekday.Contains(clock.Weekday);
        }
    }
}
