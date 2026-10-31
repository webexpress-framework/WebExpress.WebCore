using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response with a 304 Not Modified status code, indicating that the resource has not been modified since the version specified by the request headers.
    /// </summary>
    [StatusCode(304)]
    public class ResponseNotModified : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseNotModified()
        {
            Reason = "Not Modified";
        }
    }
}
