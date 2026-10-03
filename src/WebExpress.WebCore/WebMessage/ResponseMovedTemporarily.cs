using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// HTTP 302 (Found / Moved Temporarily): redirects the client to a different URL for this
    /// request only; the original URL stays valid for future requests (see RFC 2616 Section 6).
    /// </summary>
    [StatusCode(302)]
    public class ResponseMovedTemporarily : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseMovedTemporarily()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class, representing an HTTP response indicating 
        /// that the requested resource has been temporarily moved to a new location.
        /// </summary>
        /// <remarks>This response typically corresponds to the HTTP 302 status code, indicating that the
        /// resource is temporarily located at a different URI. The <paramref name="location"/> parameter is used to set
        /// the "Location" header in the response.</remarks>
        /// <param name="location">The URI of the new temporary location for the requested resource.</param>
        public ResponseMovedTemporarily(IUri location)
        {
            Reason = "Found";
            Header.Location = location?.ToString();
        }
    }
}
