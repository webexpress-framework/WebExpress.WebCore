namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// The settings of the log file. The defaults describe a server that logs to the console only,
    /// so a deployment that says nothing about logging never writes to disk by accident.
    /// </summary>
    public sealed class LogSettings
    {
        /// <summary>
        /// How the log file is written: <c>Off</c> for no file, <c>Append</c> to keep adding to the
        /// existing file or <c>Override</c> to start afresh on every start.
        /// </summary>
        /// <remarks>
        /// Kept as text rather than bound to the enum directly, so a typo falls back to the
        /// previous mode instead of failing the start-up.
        /// </remarks>
        public string Mode { get; set; } = "Off";

        /// <summary>
        /// Determines whether debug output is written as well.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// The directory the log file is created in, relative to the working directory or absolute.
        /// </summary>
        public string Path { get; set; } = "./log";

        /// <summary>
        /// The text encoding of the log file.
        /// </summary>
        public string Encoding { get; set; } = "utf-8";

        /// <summary>
        /// The file name of the log.
        /// </summary>
        public string FileName { get; set; } = "webexpress.log";

        /// <summary>
        /// The format of the timestamp in front of every entry.
        /// </summary>
        public string TimePattern { get; set; } = "dd.MM.yyyy HH:mm:ss";
    }
}
