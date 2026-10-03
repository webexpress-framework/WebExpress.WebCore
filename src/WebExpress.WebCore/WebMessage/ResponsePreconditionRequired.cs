using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 428 Precondition Required according to RFC 6585, indicating that the origin server requires the request to be conditional.
    /// </summary>
    [StatusCode(428)]
    public class ResponsePreconditionRequired : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponsePreconditionRequired()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponsePreconditionRequired(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>428</title></head><body>428 - Precondition Required</body></html>";
            Reason = "Precondition Required";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
