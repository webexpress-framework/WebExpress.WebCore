using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 405 Method Not Allowed according to RFC 7231, indicating that the request method is not supported for the target resource.
    /// </summary>
    [StatusCode(405)]
    public class ResponseMethodNotAllowed : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseMethodNotAllowed()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseMethodNotAllowed(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>405</title></head><body>405 - Method Not Allowed</body></html>";
            Reason = "Method Not Allowed";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
