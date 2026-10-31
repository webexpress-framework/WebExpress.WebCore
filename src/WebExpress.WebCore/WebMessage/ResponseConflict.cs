using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 409 Conflict according to RFC 7231, indicating that the request could not be completed due to a conflict with the current state of the target resource.
    /// </summary>
    [StatusCode(409)]
    public class ResponseConflict : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseConflict()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseConflict(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>409</title></head><body>409 - Conflict</body></html>";
            Reason = "Conflict";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
