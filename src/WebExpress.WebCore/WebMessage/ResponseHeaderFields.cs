using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Holds the header fields that are sent back to the client with a response (see RFC 2616),
    /// such as content length, content type, caching directives, and cookies. These describe the
    /// response body and control how the browser handles it.
    /// </summary>
    public class ResponseHeaderFields
    {
        /// <summary>
        /// Gets or sets the content length.
        /// </summary>
        public int ContentLength { get; set; }

        /// <summary>
        /// Gets or sets the content type.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the content language.
        /// </summary>
        public string ContentLanguage { get; set; }

        /// <summary>
        /// Gets or sets the cache control directives (see RFC 7234).
        /// </summary>
        public string CacheControl { get; set; }

        /// <summary>
        /// Gets or sets the content disposition.
        /// </summary>
        public string ContentDisposition { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the response challenges the client to
        /// authenticate with a bearer token (RFC 6750).
        /// </summary>
        /// <remarks>
        /// The challenge is never Basic: the server does not accept Basic credentials, yet a
        /// browser answers a Basic challenge with a login dialog and then sends whatever was typed
        /// with every request - and an Authorization header takes precedence over the sign-in
        /// cookie, so the user stays signed out until the browser is closed.
        /// </remarks>
        public bool WWWAuthenticate { get; set; }

        /// <summary>
        /// The challenge sent when <see cref="WWWAuthenticate"/> is set.
        /// </summary>
        internal const string BearerChallenge = "Bearer";

        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets the custom headers.
        /// </summary>
        public IDictionary<string, string> CustomHeader { get; private set; }

        /// <summary>
        /// Gets the cookies.
        /// </summary>
        public CookieCollection Cookies { get; } = [];

        /// <summary>
        /// Gets the SameSite attribute of individual cookies by name. <see cref="Cookie"/> cannot
        /// carry the attribute itself, and a cookie whose flow depends on it - a cross-site login
        /// callback, or a credential that must never leave the own site - states it here instead
        /// of inheriting the server-wide default.
        /// </summary>
        public IDictionary<string, SameSiteMode> CookieSameSite { get; } = new Dictionary<string, SameSiteMode>(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the Upgrade header (for protocol upgrade responses, e.g. "websocket").
        /// </summary>
        public string Upgrade { get; set; }

        /// <summary>
        /// Returns the connection. Keep-Alive or close.
        /// </summary>
        public string Connection { get; set; }

        /// <summary>
        /// Gets or sets the value of the Sec-WebSocket-Accept header used in 
        /// the WebSocket handshake response.
        /// </summary>
        public string SecWebSocketAccept { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ResponseHeaderFields()
        {
            // set defaults
            CustomHeader = new Dictionary<string, string>();
            WWWAuthenticate = false;
            ContentLength = -1;
        }

        /// <summary>
        /// Adds a custom header.
        /// </summary>
        /// <param name="key">The header key.</param>
        /// <param name="value">The header value.</param>
        public void AddCustomHeader(string key, string value)
        {
            if (!CustomHeader.ContainsKey(key))
            {
                CustomHeader.Add(key, value);
            }
            else
            {
                CustomHeader[key] = value;
            }
        }

        /// <summary>
        /// Converts the response header fields to a string representation.
        /// </summary>
        /// <returns>A string representation of the response header fields.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(ContentType))
            {
                sb.AppendLine("Content-Type: " + ContentType);
            }

            if (ContentLength > -1)
            {
                sb.AppendLine("Content-Length:" + ContentLength);
            }

            if (!string.IsNullOrWhiteSpace(ContentDisposition))
            {
                sb.AppendLine("Content-Disposition: " + ContentDisposition);
            }

            if (!string.IsNullOrWhiteSpace(CacheControl))
            {
                sb.AppendLine("Cache-Control: " + CacheControl);
            }

            if (WWWAuthenticate)
            {
                sb.AppendLine("WWW-Authenticate: " + BearerChallenge);
            }

            if (!string.IsNullOrWhiteSpace(Location))
            {
                sb.AppendLine("Location: " + Location);
            }

            if (!string.IsNullOrWhiteSpace(Connection))
            {
                sb.AppendLine("Connection: " + Connection);
            }

            if (!string.IsNullOrWhiteSpace(Upgrade))
            {
                sb.AppendLine("Upgrade: " + Upgrade);
            }

            if (!string.IsNullOrWhiteSpace(SecWebSocketAccept))
            {
                sb.AppendLine("Sec-WebSocket-Accept: " + SecWebSocketAccept);
            }

            foreach (var c in CustomHeader)
            {
                sb.AppendLine(c.Key + ": " + c.Value);
            }

            return sb.ToString();
        }
    }
}
