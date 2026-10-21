using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response with a 416 Range Not Satisfiable status code, indicating that the range the client asked for lies outside of the resource.
    /// </summary>
    [StatusCode(416)]
    public class ResponseRangeNotSatisfiable : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseRangeNotSatisfiable()
        {
            Reason = "Range Not Satisfiable";
        }
    }
}
