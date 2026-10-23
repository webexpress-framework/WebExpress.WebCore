namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Makes transport protection explicit so a server cannot silently downgrade delivery.
    /// </summary>
    public enum EmailSecurity
    {
        /// <summary>
        /// Allows plaintext for a deliberately configured local development relay.
        /// </summary>
        None,

        /// <summary>
        /// Requires a successful STARTTLS upgrade before authentication or submission.
        /// </summary>
        StartTls,

        /// <summary>
        /// Requires TLS from the beginning of the connection, commonly on port 465.
        /// </summary>
        SslOnConnect
    }
}
