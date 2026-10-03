using System;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Bounds a discovered metric component without requiring applications to register callbacks.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class MetricTimeoutAttribute : Attribute
    {
        /// <summary>
        /// Gets the positive time budget expressed in milliseconds.
        /// </summary>
        public int Milliseconds { get; }

        /// <summary>
        /// Declares a budget that the metrics manager validates before executing the component.
        /// </summary>
        /// <param name="milliseconds">The positive maximum duration of a collection in milliseconds.</param>
        public MetricTimeoutAttribute(int milliseconds)
        {
            Milliseconds = milliseconds;
        }
    }
}
