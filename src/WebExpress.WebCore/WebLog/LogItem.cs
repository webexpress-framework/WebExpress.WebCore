using System;

namespace WebExpress.WebCore.WebLog
{
    /// <summary>
    /// A single entry in the server log. It captures one logged message together with its severity
    /// level, the source location that produced it, and the time it occurred.
    /// </summary>
    internal class LogItem
    {
        /// <summary>
        /// Level of the entry.
        /// </summary>
        private readonly LogLevel m_level;

        /// <summary>
        /// The instance (location).
        /// </summary>
        private readonly string m_instance;

        /// <summary>
        /// The log message.
        /// </summary>
        private readonly string m_message;

        /// <summary>
        /// The timestamp.
        /// </summary>
        private readonly DateTime m_timestamp;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="level">The level of the log entry.</param>
        /// <param name="instance">The module or function where the log entry originated.</param>
        /// <param name="message">The log message.</param>
        /// <param name="timePattern">The time pattern used for formatting the timestamp.</param>
        public LogItem(LogLevel level, string instance, string message, string timePattern)
        {
            m_level = level;
            m_instance = instance;
            m_message = message;
            m_timestamp = DateTime.Now;
            TimePattern = timePattern;
        }

        /// <summary>
        /// Converts the value of this instance to a string.
        /// </summary>
        /// <returns>The log entry as a string</returns>
        public override string ToString()
        {
            if (m_level != LogLevel.Separator)
            {
                return m_timestamp.ToString(TimePattern) + " " + m_level.ToString().PadRight(9, ' ') + " " + m_instance.PadRight(19, ' ')[..19] + " " + m_message;
            }
            else
            {
                return m_message;
            }
        }

        /// <summary>
        /// Gets the level of the entry.
        /// </summary>
        public LogLevel Level => m_level;

        /// <summary>
        /// Gets the instance (location).
        /// </summary>
        public string Instance => m_instance;

        /// <summary>
        /// Gets the message.
        /// </summary>
        public string Message => m_message;

        /// <summary>
        /// Gets the timestamp.
        /// </summary>
        public DateTime Timestamp => m_timestamp;

        /// <summary>
        /// Gets or set the time patterns for log entries.
        /// </summary>
        public string TimePattern { set; get; }
    };
}
