using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 415 Unsupported Media Type according to RFC 7231, indicating that the payload format is not supported by the target resource.
    /// </summary>
    [StatusCode(415)]
    public class ResponseUnsupportedMediaType : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseUnsupportedMediaType()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseUnsupportedMediaType(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>415</title></head><body>415 - Unsupported Media Type</body></html>";
            Reason = "Unsupported Media Type";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
