using System;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Bounds a discovered health component without requiring applications to register callbacks.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class HealthTimeoutAttribute : Attribute
    {
        /// <summary>
        /// Gets the positive time budget expressed in milliseconds.
        /// </summary>
        public int Milliseconds { get; }

        /// <summary>
        /// Declares a budget that the health manager validates before executing the component.
        /// </summary>
        /// <param name="milliseconds">The positive maximum duration of a check in milliseconds.</param>
        public HealthTimeoutAttribute(int milliseconds)
        {
            Milliseconds = milliseconds;
        }
    }
}
