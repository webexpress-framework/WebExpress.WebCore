using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// HTTP 404 (Not Found): sent when no resource matches the requested URI, i.e. the server has
    /// nothing to return for that address (see RFC 2616 Section 6).
    /// </summary>
    [StatusCode(404)]
    public class ResponseNotFound : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseNotFound()
            : this(null)
        {

        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseNotFound(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>404</title></head><body>404 - Not Found</body></html>";
            Reason = "Not Found";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
