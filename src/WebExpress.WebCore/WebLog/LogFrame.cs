using System;
using System.Runtime.CompilerServices;

namespace WebExpress.WebCore.WebLog
{
    /// <summary>
    /// Creates a frame of log entries.
    /// </summary>
    public class LogFrame : IDisposable
    {
        /// <summary>
        /// The status.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Method that wants to log.
        /// </summary>
        protected string Instance { get; set; }

        /// <summary>
        /// The line number.
        /// </summary>
        protected int Line { get; set; }

        /// <summary>
        /// The source file.
        /// </summary>
        protected string File { get; set; }

        /// <summary>
        /// The log entry.
        /// </summary>
        protected ILog Log { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="log">The log entry.</param>
        /// <param name="name">The name.</param>
        /// <param name="additionalHeading">An additional heading or zero.</param>
        /// <param name="instance">Method that wants to log.</param>
        /// <param name="line">The line number.</param>
        /// <param name="file">The source file.</param>
        public LogFrame(ILog log, string name, string additionalHeading = null, [CallerMemberName] string instance = null, [CallerLineNumber] int? line = null, [CallerFilePath] string file = null)
        {
            Instance = instance;
            Line = line ?? 0;
            File = file;
            Status = string.Format("{0} completed. ", name);

            Log = log;
            Log.Separator();
            Log.Info(string.Format("Starting {0}", name) + (!string.IsNullOrWhiteSpace(additionalHeading) ? " " + additionalHeading : ""), instance, line, file);
            Log.Info("".PadRight(80, '-'), instance, line, file);
        }

        /// <summary>
        /// Release unmanaged resources that were reserved during initialization.
        /// </summary>
        public virtual void Dispose()
        {
            Log.Info("".PadRight(80, '='), Instance, Line, File);
            Log.Info(Status, Instance, Line, File);
            GC.SuppressFinalize(this);
        }
    }
}
