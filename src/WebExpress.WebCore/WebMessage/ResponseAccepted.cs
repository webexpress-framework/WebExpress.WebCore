using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response with a 202 Accepted status code, indicating that the request has been accepted for processing, but the processing has not been completed.
    /// </summary>
    [StatusCode(202)]
    public class ResponseAccepted : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseAccepted()
        {
            Reason = "Accepted";
        }
    }
}
