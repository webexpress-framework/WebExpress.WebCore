namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Represents an attribute to schedule jobs based on specified time intervals.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class JobAttribute : System.Attribute, IJobAttribute
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="minute">The minute 0-59 or * for any. Comma-separated values or ranges (-) are also possible.</param>
        /// <param name="hour">The hour 0-23 or * for arbitrary. Comma-separated values or ranges (-) are also possible.</param>
        /// <param name="day">The day 1-31 or * for any. Comma-separated values or ranges (-) are also possible.</param>
        /// <param name="month">The month 1-12 or * for any. Comma-separated values or ranges (-) are also possible.</param>
        /// <param name="weekday">The weekday 0-6 (Sunday-Saturday) or * for any. Comma-separated values or ranges (-) are also possible.</param>
        public JobAttribute(string minute = "*", string hour = "*", string day = "*", string month = "*", string weekday = "*")
        {

        }
    }
}
