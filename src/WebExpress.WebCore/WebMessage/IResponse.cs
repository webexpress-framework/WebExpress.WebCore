namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Defines what every server response must expose (see RFC 2616): the header fields, the
    /// content (body), the numeric HTTP status code, and the human-readable reason phrase.
    /// </summary>
    public interface IResponse
    {
        /// <summary>
        /// Gets the response header fields.
        /// </summary>
        ResponseHeaderFields Header { get; }

        /// <summary>
        /// Gets or sets the response content.
        /// </summary>
        object Content { get; set; }

        /// <summary>
        /// Gets the status code of the response.
        /// </summary>
        int Status { get; }

        /// <summary>
        /// Gets the reason phrase of the response.
        /// </summary>
        string Reason { get; }
    }
}