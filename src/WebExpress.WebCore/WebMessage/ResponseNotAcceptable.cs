using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 406 Not Acceptable according to RFC 7231, indicating that the target resource cannot produce a response matching the acceptable values defined in the request headers.
    /// </summary>
    [StatusCode(406)]
    public class ResponseNotAcceptable : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseNotAcceptable()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseNotAcceptable(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>406</title></head><body>406 - Not Acceptable</body></html>";
            Reason = "Not Acceptable";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
