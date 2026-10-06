using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 503 Service Unavailable according to RFC 7231, indicating that the server is currently unable to handle the request due to temporary overload or maintenance.
    /// </summary>
    [StatusCode(503)]
    public class ResponseServiceUnavailable : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseServiceUnavailable()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseServiceUnavailable(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>503</title></head><body>503 - Service Unavailable</body></html>";
            Reason = "Service Unavailable";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
