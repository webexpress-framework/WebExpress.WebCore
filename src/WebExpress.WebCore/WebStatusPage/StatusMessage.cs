namespace WebExpress.WebCore.WebStatusPage
{

    /// <summary>
    /// Carries the human-readable text - plain text, encoded wherever it is shown - of a status page (for example the explanation rendered
    /// for a 404 or 500 response), so the status page can display a meaningful message to the user.
    /// </summary>
    public class StatusMessage
    {
        /// <summary>
        /// Gets the message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class with the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public StatusMessage(string message)
        {
            Message = message;
        }

        /// <summary>
        /// Writes the body of a status response: the message as encoded text, or the fallback page
        /// when there is no message.
        /// </summary>
        /// <remarks>
        /// A message is text. It routinely quotes what a request carried - a path, a parameter, the
        /// message of an exception raised over request data - and was written into the body as
        /// markup, so every caller had to encode it and one that forgot sent script to the browser.
        /// Line breaks are kept.
        /// </remarks>
        /// <param name="message">The status message, or null.</param>
        /// <param name="fallback">The page sent without a message.</param>
        /// <returns>The html body.</returns>
        public static string ToHtml(StatusMessage message, string fallback)
        {
            if (message?.Message is null)
            {
                return fallback;
            }

            var builder = new System.Text.StringBuilder("<html><body><p>");
            var lines = message.Message.Replace("\r\n", "\n").Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("<br/>");
                }

                WebHtml.HtmlText.Encode(builder, lines[i]);
            }

            builder.Append("</p></body></html>");

            return builder.ToString();
        }
    }
}
