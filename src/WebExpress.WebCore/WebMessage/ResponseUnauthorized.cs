using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a response indicating that the request requires user authentication. See RFC 2616 Section 6
    /// </summary>
    [StatusCode(401)]
    public class ResponseUnauthorized : Response
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseUnauthorized()
            : this(null)
        {

        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">The user defined status message or null.</param>
        public ResponseUnauthorized(StatusMessage message)
        {
            Reason = "Unauthorized";

            Header.WWWAuthenticate = true;

            // callers such as the json authentication endpoint and the metrics endpoint supply their own
            // body and content type, so only an explicit message produces an html body
            if (message?.Message is string content)
            {
                Header.ContentType = "text/html";
                Header.ContentLength = content.Length;
                Content = content;
            }
        }
    }
}
