using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response with a 422 Unprocessable Entity status code.
    /// </summary>
    /// <remarks>This response is typically used to indicate that the server understands the content type of
    /// the request entity, and the syntax of the request entity is correct, but it was unable to process the contained
    /// instructions.</remarks>
    [StatusCode(422)]
    public class ResponseUnprocessableEntity : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseUnprocessableEntity()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseUnprocessableEntity(StatusMessage message)
        {
            var content = message?.Message ?? "<html><head><title>422</title></head><body>422 - Unprocessable Entity</body></html>";
            Reason = "Unprocessable Entity";

            Header.ContentType = "text/html";
            Header.ContentLength = content.Length;
            Content = content;
        }
    }
}
