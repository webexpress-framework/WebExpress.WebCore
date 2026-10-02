using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response for a 303 See Other redirect according to RFC 7231, directing the client to retrieve the resource at a different URI using GET.
    /// </summary>
    [StatusCode(303)]
    public class ResponseSeeOther : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseSeeOther()
        {
            Reason = "See Other";
        }

        /// <summary>
        /// Initializes a new instance of the class with the specified location.
        /// </summary>
        /// <param name="location">The URI to which the client should be redirected.</param>
        public ResponseSeeOther(IUri location)
            : this()
        {
            Header.Location = location?.ToString();
        }
    }
}
