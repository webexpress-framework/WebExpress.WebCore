using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 408 Request Timeout according to RFC 7231, indicating that the server did not receive a complete request within the time it was prepared to wait.
    /// </summary>
    [StatusCode(408)]
    public class ResponseRequestTimeout : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseRequestTimeout()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseRequestTimeout(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>408</title></head><body>408 - Request Timeout</body></html>";
            Reason = "Request Timeout";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
