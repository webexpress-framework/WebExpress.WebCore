using System;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents an exception that is thrown to refuse the caller access to the requested
    /// endpoint, answered in place rather than by a redirect.
    /// </summary>
    /// <remarks>
    /// An endpoint's policies are checked before it runs, and a refusal there is answered at the
    /// address that was asked for: the forbidden page for a signed-in caller, the sign-in prompt
    /// for one who is not signed in. An endpoint that can only decide once it knows more - the
    /// record its route names, the grants on it - throws this from its processing (a page's
    /// <c>Process</c>, a fragment while the page renders) and receives the same answer, instead
    /// of redirecting to a page of its own and losing the address the caller asked for.
    /// </remarks>
    public class ForbiddenException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ForbiddenException()
            : base("Access to the requested resource is refused.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">Why access is refused; for the log, never shown to the caller.</param>
        public ForbiddenException(string message)
            : base(message)
        {
        }
    }
}
