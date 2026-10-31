using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 308 Permanent Redirect according to RFC 7538, directing the client to repeat the request at a different URI while preserving the request method.
    /// </summary>
    [StatusCode(308)]
    public class ResponsePermanentRedirect : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponsePermanentRedirect()
        {
            Reason = "Permanent Redirect";
        }

        /// <summary>
        /// Initializes a new instance of the class with the specified location.
        /// </summary>
        /// <param name="location">The URI to which the client should be redirected.</param>
        public ResponsePermanentRedirect(IUri location)
            : this()
        {
            Header.Location = location?.ToString();
        }
    }
}
