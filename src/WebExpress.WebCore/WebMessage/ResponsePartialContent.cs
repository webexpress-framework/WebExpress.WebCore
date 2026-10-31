using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response with a 206 Partial Content status code, indicating that the server is delivering only part of the resource due to a range header sent by the client.
    /// </summary>
    [StatusCode(206)]
    public class ResponsePartialContent : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponsePartialContent()
        {
            Reason = "Partial Content";
        }
    }
}
