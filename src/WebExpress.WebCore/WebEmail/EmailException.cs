using System;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Keeps public error handling stable while retaining provider diagnostics for trusted callers.
    /// </summary>
    public sealed class EmailException : Exception
    {
        /// <summary>
        /// Gets the category applications can handle without inspecting exception text.
        /// </summary>
        public EmailError Error { get; }

        /// <summary>
        /// Creates a sanitized error while preserving the original diagnostic cause.
        /// </summary>
        /// <param name="error">The stable failure category.</param>
        /// <param name="innerException">The diagnostic cause, which must not be exposed to clients.</param>
        public EmailException(EmailError error, Exception innerException = null)
            : base($"Email submission failed ({error}).", innerException)
        {
            Error = error;
        }
    }
}
