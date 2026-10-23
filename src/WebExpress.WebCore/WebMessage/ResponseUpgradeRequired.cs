using System.Text;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// HTTP 426 (Upgrade Required): tells the client it must switch to a different protocol
    /// (such as a WebSocket connection) before the request can be served
    /// (see RFC 7231 Section 6.5.15).
    /// </summary>
    [StatusCode(426)]
    public class ResponseUpgradeRequired : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseUpgradeRequired()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseUpgradeRequired(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>426</title></head><body>426 - Upgrade Required</body></html>";

            // reason phrase for 426 (Upgrade Required)
            Reason = "Upgrade Required";

            Header.ContentType = "text/html";
            // set content length in bytes using UTF-8 encoding
            Header.ContentLength = Encoding.UTF8.GetByteCount(content);
            Content = content;
        }
    }
}
