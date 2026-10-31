using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 501 Not Implemented according to RFC 7231, indicating that the server does not support the functionality required to fulfill the request.
    /// </summary>
    [StatusCode(501)]
    public class ResponseNotImplemented : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseNotImplemented()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseNotImplemented(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>501</title></head><body>501 - Not Implemented</body></html>";
            Reason = "Not Implemented";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
