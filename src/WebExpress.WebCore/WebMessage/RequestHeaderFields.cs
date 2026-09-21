using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents the header fields of an HTTP request as defined in RFC 2616.
    /// </summary>
    public class RequestHeaderFields
    {
        /// <summary>
        /// Gets the host.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// Gets the connection. Keep-Alive or close.
        /// </summary>
        public string Connection { get; private set; }

        /// <summary>
        /// Gets the content length.
        /// </summary>
        public long ContentLength { get; private set; }

        /// <summary>
        /// Gets the content type.
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// Gets the language of the content.
        /// </summary>
        public string ContentLanguage { get; private set; }

        /// <summary>
        /// Gets the encoding of the content.
        /// </summary>
        public Encoding ContentEncoding { get; private set; }

        /// <summary>
        /// Gets the user agent.
        /// </summary>
        public string UserAgent { get; private set; }

        /// <summary>
        /// Gets the accepted media types.
        /// </summary>
        public IEnumerable<string> Accept { get; private set; }

        /// <summary>
        /// Gets the accepted encodings.
        /// </summary>
        public string AcceptEncoding { get; private set; }

        /// <summary>
        /// Gets the accepted languages.
        /// </summary>
        public IEnumerable<string> AcceptLanguage { get; private set; }

        /// <summary>
        /// Returns the access data name and password.
        /// </summary>
        public RequestAuthorization Authorization { get; private set; }

        /// <summary>
        /// Gets the cookies.
        /// </summary>
        public IEnumerable<Cookie> Cookies { get; } = [];

        /// <summary>
        /// Gets the referer. The referer header echoes the absolute or partial address from
        /// which a resource was requested. The Referer header allows a server to identify referring
        /// pages from which people visit or where requested resources are used.
        /// </summary>
        public string Referer { get; private set; }

        /// <summary>
        /// Allows cookie-authenticated mutations to reject cross-origin browser requests.
        /// </summary>
        public string Origin { get; private set; }

        /// <summary>
        /// Requires a browser preflight before another origin can invoke authentication mutations.
        /// </summary>
        public string AuthenticationRequest { get; private set; }

        /// <summary>
        /// Gets the If-None-Match header value. It carries the entity tag (ETag) the client already
        /// holds and is used for conditional requests so unchanged resources can be answered with 304.
        /// </summary>
        public string IfNoneMatch { get; private set; }

        /// <summary>
        /// Gets the upgrade header (e.g. "websocket" for WebSocket upgrades).
        /// </summary>
        public string Upgrade { get; private set; }

        /// <summary>
        /// Gets the Sec-WebSocket-Key header value if present.
        /// </summary>
        public string SecWebSocketKey { get; private set; }

        /// <summary>
        /// Gets the Sec-WebSocket-Protocol header value if present.
        /// </summary>
        public string SecWebSocketProtocol { get; private set; }

        /// <summary>
        /// Gets the Sec-WebSocket-Version header value if present.
        /// </summary>
        public string SecWebSocketVersion { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="contextFeatures">Initial set of features.</param>
        internal RequestHeaderFields(IFeatureCollection contextFeatures)
        {
            var requestFeature = contextFeatures.Get<IHttpRequestFeature>();

            Host = requestFeature.Headers.Host;
            Connection = requestFeature.Headers.Connection;
            ContentType = requestFeature.Headers.ContentType;
            ContentLength = requestFeature.Headers.ContentLength ?? 0;
            ContentLanguage = requestFeature.Headers.ContentLanguage;
            ContentEncoding = requestFeature.Headers.ContentEncoding.Count != 0 ? Encoding.GetEncoding(requestFeature.Headers.ContentEncoding) : Encoding.Default;
            Accept = requestFeature.Headers.Accept;
            AcceptEncoding = requestFeature.Headers.AcceptEncoding;
            AcceptLanguage = requestFeature.Headers.AcceptLanguage.SelectMany(x => x.Split(';', StringSplitOptions.RemoveEmptyEntries));
            UserAgent = requestFeature.Headers.UserAgent;
            Referer = requestFeature.Headers.Referer;
            Origin = requestFeature.Headers.Origin;
            AuthenticationRequest = requestFeature.Headers["X-WebExpress-Auth"];
            IfNoneMatch = requestFeature.Headers.IfNoneMatch;
            Upgrade = requestFeature.Headers.Upgrade;
            SecWebSocketKey = requestFeature.Headers.SecWebSocketKey;
            SecWebSocketProtocol = requestFeature.Headers.SecWebSocketProtocol;
            SecWebSocketVersion = requestFeature.Headers.SecWebSocketVersion;

            Cookies = requestFeature.Headers.Cookie
                .SelectMany(c => c.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(c =>
                {
                    var eqIndex = c.IndexOf('=');
                    if (eqIndex < 0) { return null; }
                    return new Cookie(c[..eqIndex].Trim(), c[(eqIndex + 1)..].Trim());
                })
                .Where(c => c is not null);

            Authorization = RequestAuthorization.Parse(requestFeature.Headers.Authorization);
        }
    }
}
