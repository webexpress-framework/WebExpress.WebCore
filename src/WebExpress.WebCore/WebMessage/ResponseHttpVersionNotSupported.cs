using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 505 HTTP Version Not Supported according to RFC 7231, indicating that the server does not support the HTTP protocol version used in the request.
    /// </summary>
    [StatusCode(505)]
    public class ResponseHttpVersionNotSupported : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseHttpVersionNotSupported()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseHttpVersionNotSupported(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>505</title></head><body>505 - HTTP Version Not Supported</body></html>";
            Reason = "HTTP Version Not Supported";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
