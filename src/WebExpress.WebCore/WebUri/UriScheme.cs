namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// The scheme (protocol) at the start of a URI — the part before the colon, such as
    /// <c>http</c> in <c>http://example.com</c>. It tells the client how to reach the resource.
    /// </summary>
    public enum UriScheme
    {
        /// <summary>
        /// The File URI scheme.
        /// </summary>
        File,

        /// <summary>
        /// The FTP URI scheme.
        /// </summary>
        FTP,

        /// <summary>
        /// The HTTP URI scheme.
        /// </summary>
        Http,

        /// <summary>
        /// The HTTPS URI scheme.
        /// </summary>
        Https,

        /// <summary>
        /// The LDAP URI scheme.
        /// </summary>
        Ldap,

        /// <summary>
        /// The LDAPS URI scheme.
        /// </summary>
        Ldaps,

        /// <summary>
        /// The Mailto URI scheme.
        /// </summary>
        Mailto,

        /// <summary>
        /// Represents a web service or related functionality.
        /// </summary>
        Ws,

        /// <summary>
        /// Specifies the WebSocket Secure (WSS) protocol, which provides encrypted 
        /// communication over WebSockets using TLS.
        /// </summary>
        Wss
    }

    /// <summary>
    /// Extension methods for the <see cref="UriScheme"/> enum.
    /// </summary>
    public static class UriSchemeExtension
    {
        /// <summary>
        /// Converts the <see cref="UriScheme"/> to its canonical lowercase string representation.
        /// </summary>
        /// <remarks>
        /// This is intentionally not named <c>ToString</c>: an extension method cannot override the
        /// enum's built-in <see cref="object.ToString"/>, so such a method would never be invoked
        /// through normal call syntax and would silently be dead code.
        /// </remarks>
        /// <param name="scheme">The URI scheme to convert.</param>
        /// <returns>The canonical scheme token (e.g. "https").</returns>
        public static string ToSchemeString(this UriScheme scheme)
        {
            return scheme switch
            {
                UriScheme.File => "file",
                UriScheme.FTP => "ftp",
                UriScheme.Http => "http",
                UriScheme.Https => "https",
                UriScheme.Ldap => "ldap",
                UriScheme.Ldaps => "ldaps",
                UriScheme.Mailto => "mailto",
                UriScheme.Ws => "ws",
                UriScheme.Wss => "wss",
                _ => "http"
            };
        }

        /// <summary>
        /// Returns the well-known default port for the scheme.
        /// The default port is omitted from the rendered authority so that, for example,
        /// "https://example.com:443" collapses to "https://example.com".
        /// </summary>
        /// <param name="scheme">The URI scheme.</param>
        /// <returns>The default port, or <c>-1</c> for schemes that do not carry a port.</returns>
        public static int DefaultPort(this UriScheme scheme)
        {
            return scheme switch
            {
                UriScheme.FTP => 21,
                UriScheme.Http => 80,
                UriScheme.Https => 443,
                UriScheme.Ldap => 389,
                UriScheme.Ldaps => 636,
                UriScheme.Ws => 80,
                UriScheme.Wss => 443,
                _ => -1
            };
        }
    }
}