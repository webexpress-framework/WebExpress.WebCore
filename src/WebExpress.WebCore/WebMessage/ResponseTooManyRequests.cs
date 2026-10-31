using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 429 Too Many Requests according to RFC 6585, indicating that the user has sent too many requests in a given amount of time.
    /// </summary>
    [StatusCode(429)]
    public class ResponseTooManyRequests : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseTooManyRequests()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseTooManyRequests(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>429</title></head><body>429 - Too Many Requests</body></html>";
            Reason = "Too Many Requests";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
