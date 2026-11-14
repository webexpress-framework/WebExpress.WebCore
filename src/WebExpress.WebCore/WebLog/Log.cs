using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
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

        // the console is slow and serialized, so a request thread only queues its line; one
        // background writer drains the queue. Bounded so a console that cannot keep up slows the
        // callers down instead of growing memory without limit.
        private static readonly Channel<ConsoleLine> _console = Channel.CreateBounded<ConsoleLine>
        (
            new BoundedChannelOptions(10_000) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait }
        );
        private static readonly TimeSpan _consoleDrainTimeout = TimeSpan.FromSeconds(5);
        private const int _consoleBatchSize = 256;

        // the background writer and the direct fallback must not interleave their color changes
        private static readonly Lock _consoleWriteLock = new();

        // started last, so everything the writer touches is initialized before it runs
        private static readonly Task _consoleWriter = Task.Run(WriteConsoleAsync);

        // the class part of the origin column comes from the caller's file name; resolving it once
        // per file keeps the string work off the logging path
        private static readonly ConcurrentDictionary<string, string> _callerTypes = new();

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
        /// Hooks the console drain into process shutdown. This happens here and not in the writer
        /// task, so the hooks are in place before the first entry is queued, even if the process
        /// ends before the thread pool ever started the writer.
        /// </summary>
        static Log()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushConsole();

            // a crash skips ProcessExit, and the entries describing it are the ones that matter
            AppDomain.CurrentDomain.UnhandledException += (_, _) => FlushConsole();
        }

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
        /// Queues a single, color-coded entry for the console writer. The queue keeps the order
        /// of the calls, so entries never interleave or swap colors. No level waits until its
        /// entry is on the console: an entry is logged where the code goes on - in a request or
        /// a shutdown drain - and a wait there is a round trip through the writer the whole
        /// process shares, which a slow console stretches to seconds. Closing the log, a regular
        /// exit and a crash drain the queue.
        /// </summary>
        /// <param name="level">The level of the entry, which selects the console color.</param>
        /// <param name="text">The fully formatted entry text.</param>
        private void WriteToConsole(LogLevel level, string text)
        {
            var color = level switch
            {
                LogLevel.Error or LogLevel.FatalError or LogLevel.Exception => ConsoleColor.Red,
                LogLevel.Warning => ConsoleColor.Yellow,
                _ => (ConsoleColor?)null
            };

            var line = new ConsoleLine
            (
                color,
                level != LogLevel.Exception && text.Length > _separatorWidth ? string.Concat(text.AsSpan(0, _separatorWidth - 3), "...") : text.PadRight(_width, ' '),
                null
            );

            if (!Enqueue(line))
            {
                // the writer is gone or stuck; writing directly keeps the entry and, above all,
                // keeps the caller from waiting on a queue nobody drains any more
                WriteBlock(color, line.Text + Environment.NewLine);
            }
        }

        /// <summary>
        /// Hands an item to the console writer. A full queue slows the caller down, but only as
        /// long as the writer is alive and makes progress within the drain timeout.
        /// </summary>
        /// <param name="line">The entry or flush marker to queue.</param>
        /// <returns>True if the item was queued, false if the writer cannot take it.</returns>
        private static bool Enqueue(ConsoleLine line)
        {
            if (_console.Writer.TryWrite(line))
            {
                return true;
            }

            if (_consoleWriter.IsCompleted)
            {
                return false;
            }

            // a canceled write is never queued, so a timed-out entry cannot show up twice
            using var timeout = new CancellationTokenSource(_consoleDrainTimeout);

            try
            {
                _console.Writer.WriteAsync(line, timeout.Token).AsTask().GetAwaiter().GetResult();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// Waits until every console entry queued so far has been written. Used where the output
        /// must be complete - when logging stops and before the process exits. Queuing the marker
        /// and waiting for it share one timeout, so a stalled console delays shutdown by at most
        /// that long.
        /// </summary>
        public static void FlushConsole()
        {
            if (_consoleWriter.IsCompleted)
            {
                return;
            }

            using var timeout = new CancellationTokenSource(_consoleDrainTimeout);
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var marker = new ConsoleLine(null, null, done);

            try
            {
                if (!_console.Writer.TryWrite(marker))
                {
                    _console.Writer.WriteAsync(marker, timeout.Token).AsTask().GetAwaiter().GetResult();
                }

                done.Task.Wait(timeout.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Drains the console queue for the lifetime of the process. Consecutive entries of one
        /// color are written as a single block, so the color is switched once per block and not
        /// once per entry.
        /// </summary>
        private static async Task WriteConsoleAsync()
        {
            var reader = _console.Reader;
            var batch = new List<ConsoleLine>(_consoleBatchSize);
            var block = new StringBuilder();

            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (batch.Count < _consoleBatchSize && reader.TryRead(out var line))
                {
                    batch.Add(line);
                }

                var index = 0;
                while (index < batch.Count)
                {
                    if (batch[index].Done is { } done)
                    {
                        done.TrySetResult();
                        index++;
                        continue;
                    }

                    var color = batch[index].Color;
                    block.Clear();

                    while (index < batch.Count && batch[index].Done is null && batch[index].Color == color)
                    {
                        block.AppendLine(batch[index].Text);
                        index++;
                    }

                    try
                    {
                        WriteBlock(color, block.ToString());
                    }
                    catch (Exception ex)
                    {
                        // only this block is lost; the rest of the batch and every waiting flush
                        // still get their turn, so a closed or redirected console cannot stop the writer
                        ReportConsoleFailure(ex);
                    }
                }

                batch.Clear();
            }
        }

        /// <summary>
        /// Writes a block of entries in one color.
        /// </summary>
        /// <param name="color">The color, or null for the console's default.</param>
        /// <param name="text">The entries, each terminated by a line break.</param>
        private static void WriteBlock(ConsoleColor? color, string text)
        {
            lock (_consoleWriteLock)
            {
                try
                {
                    // a default block resets as well: a color left behind by another component
                    // would otherwise tint every info entry after it
                    if (color is null)
                    {
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = color.Value;
                    }

                    Console.Out.Write(text);
                }
                finally
                {
                    if (color is not null)
                    {
                        Console.ResetColor();
                    }
                }
            }
        }

        /// <summary>
        /// Reports that console output was lost. The notice goes to the error stream, because the
        /// output stream is the one that just failed.
        /// </summary>
        /// <param name="exception">The failure of the console write.</param>
        private static void ReportConsoleFailure(Exception exception)
        {
            try
            {
                Console.Error.WriteLine($"Logging failed: {exception.Message}");
            }
            catch
            {
                // nothing is left to report to
            }
        }

        /// <summary>
        /// Builds the origin column of an entry from the caller's class and member. The class is
        /// taken from the source file name the compiler passes in; walking the stack at run time
        /// for it cost more than the rest of the logging call. The price is that a nested class,
        /// or a second class in the same file, is reported under the file's main class.
        /// </summary>
        /// <param name="instance">The calling member, or an explicit origin.</param>
        /// <param name="file">The caller's source file, or null when the origin is explicit.</param>
        /// <returns>The origin in the form class.member.</returns>
        private static string Caller(string instance, string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return instance;
            }

            var type = _callerTypes.GetOrAdd(file, static path =>
            {
                // both separators: the path is the one of the build machine, not of this one
                var start = path.LastIndexOfAny(['/', '\\']) + 1;

                // the name ends before the extension, a partial part (Foo.Render.cs) or the
                // type parameters of a generic file name (Foo{T}.cs, Foo`1.cs)
                var end = path.IndexOfAny(['.', '{', '`'], start);

                return end < 0 ? path[start..] : path[start..end];
            });

            return $"{type}.{instance}";
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
                Add(LogLevel.Info, message, Caller(instance, file), line, file);
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
                Add(LogLevel.Info, string.Format(message, args), Caller(instance, file), line, file);
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
                Add(LogLevel.Warning, message, Caller(instance, file), line, file);
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
                Add(LogLevel.Warning, string.Format(message, args), Caller(instance, file), line, file);
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
                Add(LogLevel.Error, message, Caller(instance, file), line, file);
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
                Add(LogLevel.Error, string.Format(message, args), Caller(instance, file), line, file);
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
                Add(LogLevel.FatalError, message, Caller(instance, file), line, file);
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
                Add(LogLevel.FatalError, string.Format(message, args), Caller(instance, file), line, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug-Logging failed: {ex.Message}");
            }
            Interlocked.Increment(ref _errorCount);
        }

        /// <summary>
        /// Preserves exception types, nested causes and stack traces for failure diagnosis.
        /// </summary>
        /// <param name="exception">The exception whose complete diagnostic details are required.</param>
        /// <param name="instance">>Method/ function that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public void Exception(Exception exception, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            try
            {
                Add(LogLevel.Exception, exception?.ToString(), Caller(instance, file), line, file);
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
            // debug entries are frequent and usually switched off; skip the formatting entirely
            if (!DebugMode)
            {
                return;
            }

            try
            {
                Add(LogLevel.Debug, message, Caller(instance, file), line, file);
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
            // see the parameterless-args overload: avoid the formatting unless debug output is on
            if (!DebugMode)
            {
                return;
            }

            try
            {
                Add(LogLevel.Debug, string.Format(message, args), Caller(instance, file), line, file);
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
            FlushConsole();
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

        /// <summary>
        /// An entry waiting for the console writer, or - with a completion set - a marker that is
        /// completed once everything queued before it has been written.
        /// </summary>
        /// <param name="Color">The color of the entry, or null for the console's default.</param>
        /// <param name="Text">The formatted entry.</param>
        /// <param name="Done">The completion of a flush marker.</param>
        private sealed record ConsoleLine(ConsoleColor? Color, string Text, TaskCompletionSource Done);
    }
}
