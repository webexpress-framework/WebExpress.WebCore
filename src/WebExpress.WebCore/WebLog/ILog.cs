using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebLog
{
    /// <summary>
    /// Interface for logging events to your log file
    /// 
    /// The program writes a variety of information to an event log file. The log 
    /// is stored in the log directory. The name consists of the date and the ending ".log". 
    /// The structure is designed in such a way that the log file can be read and analyzed with a text editor.
    /// Error messages and notes are made available persistently in the log, so the event log files 
    /// are suitable for error analysis and for checking the correct functioning of the program. The minutes 
    /// are organized in tabular form. In the first column, the primeval time is indicated. The second 
    /// column defines the level of the log entry. The third column lists the function that produced the entry. 
    /// The last column indicates a note or error description.
    /// </summary>
    /// <code>
    /// Example:
    /// 08:26:30 Info      Program.Main                   Startup
    /// 08:26:30 Info      Program.Main                   --------------------------------------------------
    /// 08:26:30 Info      Program.Main                   Version: 0.0.0.1
    /// 08:26:30 Info      Program.Main                   Arguments: -test
    /// 08:26:30 Info      Program.Main                   Configuration version: V1
    /// 08:26:30 Info      Program.Main                   Processing: sequentiell
    /// </code>
    public interface ILog : ILogger
    {
        /// <summary>
        /// Gets or sets the encoding.
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Determines whether to display debug output.
        /// </summary>
        public bool DebugMode { get; }

        /// <summary>
        /// Gets or sets the file name of the log
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Gets the number of exceptions.
        /// </summary>
        public int ExceptionCount { get; }

        /// <summary>
        /// Gets the number of errors (errors + exceptions).
        /// </summary>
        public int ErrorCount { get; }

        /// <summary>
        /// Gets the number of warnings.
        /// </summary>
        public int WarningCount { get; }

        /// <summary>
        /// Checks if the log has been opened for writing.
        /// </summary>
        public bool IsOpen { get; }

        /// <summary>
        /// Gets or sets the log mode.
        /// </summary>
        public LogMode LogMode { get; set; }

        /// <summary>
        /// Gets the default instance of the logger.
        /// </summary>
        public static ILog Current { get; }

        /// <summary>
        /// Gets or sets the file name patterns.
        /// </summary>
        public string FilePattern { set; get; }

        /// <summary>
        /// Gets or sets the time patternsspecifying log entries.
        /// </summary>
        public string TimePattern { set; get; }

        /// <summary>
        /// Gets or sets the maximum number of recent log entries retained in memory for live
        /// inspection. The retained entries are independent of whether a log file is written.
        /// </summary>
        public int RecentCapacity { get; set; }

        /// <summary>
        /// Occurs immediately after a log entry has been recorded. Handlers run on the calling
        /// (logging) thread and must therefore be fast and must not throw; a faulty handler is
        /// isolated so it cannot break logging.
        /// </summary>
        public event EventHandler<LogEntry> EntryLogged;

        /// <summary>
        /// Returns a snapshot of the most recent log entries currently retained in memory, oldest first.
        /// </summary>
        /// <returns>A point-in-time copy that is safe to enumerate without further locking.</returns>
        public IReadOnlyList<LogEntry> GetRecentEntries();

        /// <summary>
        /// Starts logging
        /// </summary>
        /// <param name="path">The path where the log file is created.</param>
        /// <param name="name">The file name of the log file.</param>
        public void Begin(string path, string name);

        /// <summary>
        /// Starts logging
        /// </summary>
        /// <param name="path">The path where the log file is created.</param>
        public void Begin(string path);

        /// <summary>
        /// Starts logging
        /// </summary>
        /// <param name="settings">The log settings</param>
        public void Begin(LogSettings settings);

        /// <summary>
        /// A dividing line with * characters
        /// </summary>
        public void Separator();

        /// <summary>
        /// A separator with custom characters
        /// </summary>
        /// <param name="sepChar">The separator.</param>
        public void Separator(char sepChar);

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Info(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null);

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Info(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Warning(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Warning(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Error(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Error(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void FatalError(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void FatalError(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args);

        /// <summary>
        /// Logs an exception message.
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Exception(Exception ex, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Debug(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Debug(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args);

        /// <summary>
        /// Stops logging.
        /// </summary>
        public void Close();

        /// <summary>
        /// Cleans up the log.
        /// </summary>
        public void Clear();

        /// <summary>
        /// Writes the contents of the queue to the log.
        /// </summary>
        public void Flush();
    }
}
