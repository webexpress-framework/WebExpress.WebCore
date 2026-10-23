using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for the HTTP 500 Internal Server Error status code according to RFC 2616 Section 6.
    /// </summary>
    [StatusCode(500)]
    public class ResponseInternalServerError : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseInternalServerError()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseInternalServerError(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>500</title></head><body>500 - Internal Server Error</body></html>";
            Reason = "Internal Server Error";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
