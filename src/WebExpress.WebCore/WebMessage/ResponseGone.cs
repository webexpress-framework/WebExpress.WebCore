using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 410 Gone according to RFC 7231, indicating that the target resource is no longer available and this condition is likely permanent.
    /// </summary>
    [StatusCode(410)]
    public class ResponseGone : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseGone()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseGone(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>410</title></head><body>410 - Gone</body></html>";
            Reason = "Gone";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
