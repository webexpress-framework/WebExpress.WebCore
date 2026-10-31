using System.Reflection;
using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Common base class for the message a server sends back to the client (see RFC 2616).
    /// It holds the response header fields, the content (body), and the reason phrase. The numeric
    /// HTTP status code is taken from the <c>StatusCode</c> attribute on each concrete response
    /// type, so subclasses such as <see cref="ResponseOK"/> or <see cref="ResponseNotFound"/> only
    /// need to declare their status and fill in the content.
    /// </summary>
    public abstract class Response : IResponse
    {
        /// <summary>
        /// Gets the response header fields.
        /// </summary>
        public ResponseHeaderFields Header { get; } = new ResponseHeaderFields();

        /// <summary>
        /// Gets or sets the response content.
        /// </summary>
        public object Content { get; set; }

        /// <summary>
        /// Gets the status code of the response.
        /// </summary>
        public int Status => GetType().GetCustomAttribute<StatusCodeAttribute>().StatusCode;

        /// <summary>
        /// Gets the reason phrase of the response.
        /// </summary>
        public string Reason { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the Response class.
        /// </summary>
        protected Response()
        {
        }

        /// <summary>
        /// Appends the specified content type to the existing Content-Type header value.
        /// </summary>
        /// <param name="contentType">The content type to append. Cannot be null or empty.</param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public Response AddHeaderContentType(string contentType)
        {
            Header.ContentType = contentType?.Trim();

            return this;
        }
    }
}
