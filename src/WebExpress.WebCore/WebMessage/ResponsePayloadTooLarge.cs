using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents an HTTP 413 (Payload Too Large) response.
    /// According to RFC 7231, section 6.5.11, this status code indicates
    /// that the server is refusing to process a request because its payload
    /// exceeds the size limits defined by the server.
    /// </summary>
    [StatusCode(413)]
    public class ResponsePayloadTooLarge : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponsePayloadTooLarge()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class
        /// with an optional user-defined status message.
        /// </summary>
        /// <param name="message">The user-defined status message, or <c>null</c> to use a default HTML body.</param>
        public ResponsePayloadTooLarge(StatusMessage message)
        {
            var content = StatusMessage.ToHtml(message,
                "<html><head><title>413 Payload Too Large</title></head><body>413 - Payload Too Large</body></html>");

            Reason = "Payload Too Large";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
