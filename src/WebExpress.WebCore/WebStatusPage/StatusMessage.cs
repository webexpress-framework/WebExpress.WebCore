namespace WebExpress.WebCore.WebStatusPage
{

    /// <summary>
    /// Carries the human-readable text shown on a status page (for example the explanation rendered
    /// for a 404 or 500 response), so the status page can display a meaningful message to the user.
    /// </summary>
    public class StatusMessage
    {
        /// <summary>
        /// Gets the message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class with the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public StatusMessage(string message)
        {
            Message = message;
        }
    }
}
