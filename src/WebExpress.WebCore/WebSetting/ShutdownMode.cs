namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Defines whether container termination allows admitted work to finish.
    /// </summary>
    public enum ShutdownMode
    {
        /// <summary>
        /// Aborts connections and proceeds to cleanup without a drain interval.
        /// </summary>
        Immediate,

        /// <summary>
        /// Allows admitted work to finish within the configured shutdown budget.
        /// </summary>
        Graceful
    }
}
