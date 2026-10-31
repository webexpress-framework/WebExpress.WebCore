using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents an HTTP response with a 204 No Content status code.
    /// </summary>
    /// <remarks>This response indicates that the request was successfully processed, but no content is
    /// returned in the response body. Use this class to represent operations where no additional data needs to be
    /// conveyed to the client.</remarks>
    [StatusCode(204)]
    public class ResponseNoContent : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseNoContent()
        {
            Reason = "No Content";
        }
    }
}
