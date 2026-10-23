using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 504 Gateway Timeout according to RFC 7231, indicating that the server, while acting as a gateway or proxy, did not receive a timely response from an upstream server.
    /// </summary>
    [StatusCode(504)]
    public class ResponseGatewayTimeout : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseGatewayTimeout()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseGatewayTimeout(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>504</title></head><body>504 - Gateway Timeout</body></html>";
            Reason = "Gateway Timeout";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
