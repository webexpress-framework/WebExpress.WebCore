using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 502 Bad Gateway according to RFC 7231, indicating that the server, while acting as a gateway or proxy, received an invalid response from an inbound server.
    /// </summary>
    [StatusCode(502)]
    public class ResponseBadGateway : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseBadGateway()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseBadGateway(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>502</title></head><body>502 - Bad Gateway</body></html>";
            Reason = "Bad Gateway";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
