using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// HTTP 200 (OK): the standard answer telling the client the request succeeded and the
    /// result is contained in the response body (see RFC 2616 Section 6).
    /// </summary>
    [StatusCode(200)]
    public class ResponseOK : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseOK()
        {
            Reason = "OK";
        }
    }
}
