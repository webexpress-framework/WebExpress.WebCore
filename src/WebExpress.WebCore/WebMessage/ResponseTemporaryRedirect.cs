using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 307 Temporary Redirect according to RFC 7231, directing the client to repeat the request at a different URI while preserving the request method.
    /// </summary>
    [StatusCode(307)]
    public class ResponseTemporaryRedirect : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseTemporaryRedirect()
        {
            Reason = "Temporary Redirect";
        }

        /// <summary>
        /// Initializes a new instance of the class with the specified location.
        /// </summary>
        /// <param name="location">The URI to which the client should be redirected.</param>
        public ResponseTemporaryRedirect(IUri location)
            : this()
        {
            Header.Location = location?.ToString();
        }
    }
}
