namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Distinguishes provider acceptance from suppression of a previous delivery attempt.
    /// </summary>
    public enum EmailSendResult
    {
        /// <summary>
        /// Indicates provider acceptance, which does not guarantee arrival in the recipient inbox.
        /// </summary>
        Accepted,

        /// <summary>
        /// Indicates an existing claim, including attempts still running or with an unknown outcome.
        /// </summary>
        AlreadyAttempted
    }
}
