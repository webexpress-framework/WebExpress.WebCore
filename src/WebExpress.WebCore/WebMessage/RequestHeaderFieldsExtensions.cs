using System;
using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Provides extension methods for converting strongly typed request header 
    /// fields to formats suitable for WebSocket handshake processing.
    /// </summary>
    public static class RequestHeaderFieldsExtensions
    {
        /// <summary>
        /// Converts the strongly typed RequestHeaderFields into a dictionary
        /// suitable for WebSocket handshake processing.
        /// </summary>
        public static Dictionary<string, string> ToDictionary(this RequestHeaderFields header)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Add(string name, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    dict[name] = value;
                }
            }

            // standard http headers
            Add("Host", header.Host);
            Add("Connection", header.Connection);
            Add("Content-Type", header.ContentType);
            Add("Content-Length", header.ContentLength > 0 ? header.ContentLength.ToString() : null);
            Add("Content-Language", header.ContentLanguage);
            Add("Content-Encoding", header.ContentEncoding);
            Add("User-Agent", header.UserAgent);
            Add("Referer", header.Referer);

            // accept headers
            if (header.Accept is not null)
                Add("Accept", string.Join(", ", header.Accept));

            Add("Accept-Encoding", header.AcceptEncoding);

            if (header.AcceptLanguage is not null)
                Add("Accept-Language", string.Join(", ", header.AcceptLanguage));

            // cookies
            if (header.Cookies is not null)
            {
                var cookieString = string.Join("; ", header.Cookies.Select(c => $"{c.Name}={c.Value}"));
                Add("Cookie", cookieString);
            }

            // authorization
            if (header.Authorization is not null)
                Add("Authorization", header.Authorization.ToString());

            // websocket-specific headers
            Add("Upgrade", header.Upgrade);
            Add("Sec-WebSocket-Key", header.SecWebSocketKey);
            Add("Sec-WebSocket-Protocol", header.SecWebSocketProtocol);
            Add("Sec-WebSocket-Version", header.SecWebSocketVersion);

            return dict;
        }
    }
}