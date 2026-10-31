using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 411 Length Required according to RFC 7231, indicating that the server refuses to accept the request without a defined Content-Length header.
    /// </summary>
    [StatusCode(411)]
    public class ResponseLengthRequired : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseLengthRequired()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseLengthRequired(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>411</title></head><body>411 - Length Required</body></html>";
            Reason = "Length Required";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
