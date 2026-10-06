namespace WebExpress.WebCore.WebLog
{
    /// <summary>
    /// Enumeration defines the different log levels.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// An informational message. This is generally used for informational messages that display the progress of the application at coarse-grained level.
        /// </summary>
        Info,

        /// <summary>
        /// A warning message. This is typically for situations that are not necessarily errors, but that may need attention during debugging or tuning.
        /// </summary>
        Warning,

        /// <summary>
        /// A fatal error message. This is a very severe error event that will presumably lead the application to abort.
        /// </summary>
        FatalError,

        /// <summary>
        /// An error message. This is a message indicating that an operation could not be completed.
        /// </summary>
        Error,

        /// <summary>
        /// An exception message. This is a message indicating that an exception was thrown by an operation.
        /// </summary>
        Exception,

        /// <summary>
        /// A debug message. This is a message containing low-level information for developers and system administrators for debugging purposes.
        /// </summary>
        Debug,

        /// <summary>
        /// A separator message. This is used to separate groups of messages in the logging output.
        /// </summary>
        Separator
    }
}
