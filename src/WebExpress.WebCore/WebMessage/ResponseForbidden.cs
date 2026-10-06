using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// HTTP 403 (Forbidden): sent when the server understood the request but refuses to fulfil it
    /// because the caller lacks permission, regardless of authentication (see RFC 2616 Section 6).
    /// </summary>
    [StatusCode(403)]
    public class ResponseForbidden : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseForbidden()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseForbidden(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>403</title></head><body>403 - Forbidden</body></html>";
            Reason = "Forbidden";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
