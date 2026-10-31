using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebLog
{
    /// <summary>
    /// Class for logging events to your log file
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
    public class Log : ILog
    {
        private readonly Queue<LogItem> _queue = new();
        private readonly Queue<LogEntry> _recent = new();
        private string _path;
        private Thread _workerThread;
        private const int _separatorWidth = 260;
        private volatile bool _done = false;
        private readonly int _width = 250;

        // dedicated lock objects; the previous implementation locked on the _path string, which is
        // an interned, shared instance and is null until Begin has run - both are unsafe.
        private readonly Lock _fileLock = new();
        private readonly Lock _recentLock = new();
        private static readonly Lock _consoleLock = new();

        // counters are touched from arbitrary caller threads, so they are updated atomically.
        private int _errorCount;
        private int _warningCount;
        private int _exceptionCount;

        /// <summary>
        /// Gets or sets the encoding.
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Determines whether to display debug output.
        /// </summary>
        public bool DebugMode { get; private set; } = false;

        /// <summary>
        /// Gets or sets the file name of the log
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Gets the number of exceptions.
        /// </summary>
        public int ExceptionCount => _exceptionCount;

        /// <summary>
        /// Gets the number of errors (errors + exceptions).
        /// </summary>
        public int ErrorCount => _errorCount;

        /// <summary>
        /// Gets the number of warnings.
        /// </summary>
        public int WarningCount => _warningCount;

        /// <summary>
        /// Checks if the log has been opened for writing.
        /// </summary>
        public bool IsOpen => _workerThread is not null;

        /// <summary>
        /// Gets or sets the log mode.
        /// </summary>
        public LogMode LogMode { get; set; }

        /// <summary>
        /// Gets the default instance of the logger.
        /// </summary>
        public static Log Current { get; } = new Log();

        /// <summary>
        /// Gets or sets file name patterns.
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
        public int RecentCapacity { get; set; } = 1000;

        /// <summary>
        /// Occurs immediately after a log entry has been recorded. Handlers run on the calling
        /// (logging) thread and must therefore be fast and must not throw; a faulty handler is
        /// isolated so it cannot break logging.
        /// </summary>
        public event EventHandler<LogEntry> EntryLogged;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Log()
        {
            Encoding = Encoding.UTF8;
            FilePattern = "yyyyMMdd";
            TimePattern = "yyyMMddHHmmss";
            LogMode = LogMode.Append;
        }

        /// <summary>
        /// Starts logging
        /// </summary>
        /// <param name="path">The path where the log file is created.</param>
        /// <param name="name">The file name of the log file.</param>
        public void Begin(string path, string name)
        {
            Filename = Path.Combine(path, name);
            _path = path;

            // check directory
            if (!Directory.Exists(_path))
            {
                // no log directory exists yet -create >
                Directory.CreateDirectory(_path);
            }

            // Delete existing log file when overwrite mode is active
            if (LogMode == LogMode.Override)
            {
                try
                {
                    File.Delete(Filename);
                }
                catch
                {
                }
            }

            // reset the stop flag so logging can be restarted after a previous Close
            _done = false;

            // only a single worker thread is started; calling Begin again just reconfigures the target
            if (_workerThread is null)
            {
                _workerThread = new Thread(new ThreadStart(ThreadProc))
                {
                    // Background thread
                    IsBackground = true,
                    Name = "WebExpress.Log"
                };

                _workerThread.Start();
            }
        }

        /// <summary>
        /// Starts logging
        /// </summary>
        /// <param name="path">The path where the log file is created.</param>
        public void Begin(string path)
        {
            Begin(path, DateTime.Today.ToString(FilePattern) + ".log");
        }

        /// <summary>
        /// Starts logging
        /// </summary>
        /// <param name="settings">The log settings</param>
        public void Begin(LogSettings settings)
        {
            Filename = settings.FileName;

            // a malformed configuration value must not crash startup; fall back to the current value
            if (Enum.TryParse<LogMode>(settings.Mode, true, out var mode))
            {
                LogMode = mode;
            }

            try
            {
                Encoding = Encoding.GetEncoding(settings.Encoding);
            }
            catch
            {
                Encoding = Encoding.UTF8;
            }

            if (!string.IsNullOrWhiteSpace(settings.TimePattern))
            {
                TimePattern = settings.TimePattern;
            }

            DebugMode = settings.Debug;

            Begin(settings.Path, Filename);
        }

        /// <summary>
        /// Adds a message to the log.
        /// </summary>
        /// <param name="level">The Level.</param>
        /// <param name="message">The log message.</param>
        /// <param name="instance">Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        protected virtual void Add(LogLevel level, string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            try
            {
                // split multi-line messages so every line is rendered as its own tabular entry
                foreach (var l in (message ?? string.Empty).Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
                {
                    var item = new LogItem(level, instance, l, TimePattern);

                    WriteToConsole(level, item.ToString() ?? string.Empty);

                    // only buffer for the file when a log file is actually written; otherwise the
                    // queue would grow unbounded whenever logging is switched off.
                    if (LogMode != LogMode.Off)
                    {
                        lock (_queue)
                        {
                            _queue.Enqueue(item);
                        }
                    }

                    Record(level, instance, l, item.Timestamp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logging failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes a single, color-coded entry to the console. Serialized so concurrent callers
        /// cannot interleave color changes with each other.
        /// </summary>
        /// <param name="level">The level of the entry, which selects the console color.</param>
        /// <param name="text">The fully formatted entry text.</param>
        private void WriteToConsole(LogLevel level, string text)
        {
            lock (_consoleLock)
            {
                switch (level)
                {
                    case LogLevel.Error:
                    case LogLevel.FatalError:
                    case LogLevel.Exception:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogLevel.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    default:
                        break;
                }

                Console.WriteLine(text.Length > _separatorWidth ? string.Concat(text.AsSpan(0, _separatorWidth - 3), "...") : text.PadRight(_width, ' '));
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Adds an entry to the bounded in-memory ring buffer and notifies subscribers.
        /// </summary>
        /// <param name="level">The level of the entry.</param>
        /// <param name="instance">The originating location.</param>
        /// <param name="message">The log message.</param>
        /// <param name="timestamp">The timestamp of the entry.</param>
        private void Record(LogLevel level, string instance, string message, DateTime timestamp)
        {
            var entry = new LogEntry(timestamp, level, instance, message);

            lock (_recentLock)
            {
                _recent.Enqueue(entry);

                // honor a capacity that may have been lowered at runtime
                while (_recent.Count > RecentCapacity && _recent.Count > 0)
                {
                    _recent.Dequeue();
                }
            }

            try
            {
                EntryLogged?.Invoke(this, entry);
            }
            catch
            {
                // a faulty subscriber must never break logging
            }
        }

        /// <summary>
        /// Returns a snapshot of the most recent log entries currently retained in memory, oldest first.
        /// </summary>
        /// <returns>A point-in-time copy that is safe to enumerate without further locking.</returns>
        public IReadOnlyList<LogEntry> GetRecentEntries()
        {
            lock (_recentLock)
            {
                return new List<LogEntry>(_recent);
            }
        }

        /// <summary>
        /// A dividing line with * characters
        /// </summary>
        public void Separator()
        {
            Separator('*');
        }

        /// <summary>
        /// A separator with custom characters
        /// </summary>
        /// <param name="sepChar">The separator.</param>
        public void Separator(char sepChar)
        {
            Add(LogLevel.Separator, "".PadRight(_separatorWidth, sepChar));
        }

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Info(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Info, message, $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Info(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Info, string.Format(message, args), $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Warning(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Warning, message, $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _warningCount);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Warning(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Warning, string.Format(message, args), $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _warningCount);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Error(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Error, message, $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _errorCount);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Error(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Error, string.Format(message, args), $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _errorCount);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void FatalError(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.FatalError, message, $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _errorCount);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void FatalError(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.FatalError, string.Format(message, args), $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _errorCount);
        }

        /// <summary>
        /// Logs an exception message.
        /// </summary>
        /// <param name="exception">The exception</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Exception(Exception exception, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Exception, exception?.Message.Trim(), $"{className}.{instance}", line, file);
                Add(LogLevel.Exception, exception?.StackTrace is not null
                    ? exception?.StackTrace.Trim()
                    : exception?.Message.Trim(), $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _exceptionCount);
            Interlocked.Increment(ref _errorCount);
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Debug(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            // capturing the call stack to derive the class name is expensive; skip it entirely when
            // debug output is disabled, which is the common case outside of troubleshooting.
            if (!DebugMode)
            {
                return;
            }

            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Debug, message, $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        /// <param name="args">Parameter für die Formatierung der Nachricht</param>
        public void Debug(string message, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null, params object[] args)
        {
            // see the parameterless-args overload: avoid the stack walk unless debug output is on.
            if (!DebugMode)
            {
                return;
            }

            try
            {
                var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
                var className = methodInfo?.ReflectedType.Name;

                Add(LogLevel.Debug, string.Format(message, args), $"{className}.{instance}", line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops logging. Signals the worker thread, waits briefly for it to finish and writes any
        /// entries that are still pending.
        /// </summary>
        public void Close()
        {
            _done = true;

            var worker = _workerThread;
            worker?.Join(TimeSpan.FromSeconds(6));
            _workerThread = null;

            Flush();
        }

        /// <summary>
        /// Cleans up the log.
        /// </summary>
        public void Clear()
        {
            Interlocked.Exchange(ref _errorCount, 0);
            Interlocked.Exchange(ref _warningCount, 0);
            Interlocked.Exchange(ref _exceptionCount, 0);
        }

        /// <summary>
        /// Writes the contents of the queue to the log.
        /// </summary>
        public void Flush()
        {
            var list = new List<LogItem>();

            // lock queue before concurrent access
            lock (_queue)
            {
                list.AddRange(_queue);
                _queue.Clear();
            }

            if (list.Count == 0 || LogMode == LogMode.Off || string.IsNullOrEmpty(Filename))
            {
                return;
            }

            // protect file writing from concurrent access; a transient IO failure must not take down
            // the background worker thread.
            try
            {
                lock (_fileLock)
                {
                    using var fs = new FileStream(Filename, FileMode.Append);
                    using var w = new StreamWriter(fs, Encoding);
                    foreach (var item in list)
                    {
                        w.WriteLine(item.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Writing the log file failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Thread Start Function
        /// </summary>
        private void ThreadProc()
        {
            while (!_done)
            {
                // poll in small steps so Close responds quickly instead of waiting a full interval
                for (var i = 0; i < 10 && !_done; i++)
                {
                    Thread.Sleep(500);
                }

                Flush();
            }
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <typeparam name="TState">The type of object to write.</typeparam>
        /// <param name="logLevel">The entry is written at this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to write. Can also be an object.</param>
        /// <param name="exception">The exception that applies to this entry.</param>
        /// <param name="formatter">Function to create a string message of the state and exception parameters.</param>
        void ILogger.Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Error)
            {
                var message = exception?.Message ?? formatter.Invoke(state, exception);
                Error(message, "Kestrel", null, null);
            }

        }

        /// <summary>
        /// Verifies that the specified logLevel parameter is enabled.
        /// </summary>
        /// <param name="logLevel">Level to be checked.</param>
        /// <returns>True in the enabled state, false otherwise.</returns>
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return true;
        }

        /// <summary>
        /// Formats the message and creates a range.
        /// </summary>
        /// <typeparam name="TState">The type of object to write.</typeparam>
        /// <param name="state">The ILogger interface in which to create the scope.</param>
        /// <returns>A disposable range object. Can be NULL.</returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }
}
