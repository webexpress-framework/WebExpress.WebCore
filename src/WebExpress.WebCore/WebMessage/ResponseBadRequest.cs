using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// HTTP 400 (Bad Request): sent when the server cannot process the request because it is
    /// malformed or invalid (for example, a broken body or missing required data).
    /// </summary>
    [StatusCode(400)]
    public class ResponseBadRequest : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseBadRequest()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseBadRequest(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>400</title></head><body>400 - Bad Request</body></html>";
            Reason = "Bad Request";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
