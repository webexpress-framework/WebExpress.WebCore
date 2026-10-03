using System;

namespace WebExpress.WebCore.WebLog
{
    /// <summary>
    /// An immutable snapshot of a single log entry. It exists so consumers (e.g. an admin log viewer
    /// or live monitoring) can read recent log activity without touching the internal, mutable write
    /// buffer of the logger.
    /// </summary>
    public sealed class LogEntry
    {
        /// <summary>
        /// Gets the point in time at which the entry was recorded.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the severity level of the entry.
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// Gets the originating location (typically "ClassName.MethodName").
        /// </summary>
        public string Instance { get; }

        /// <summary>
        /// Gets the log message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="timestamp">The point in time at which the entry was recorded.</param>
        /// <param name="level">The severity level of the entry.</param>
        /// <param name="instance">The originating location.</param>
        /// <param name="message">The log message.</param>
        public LogEntry(DateTime timestamp, LogLevel level, string instance, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Instance = instance;
            Message = message;
        }
    }
}
