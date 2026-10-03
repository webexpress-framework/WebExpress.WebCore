using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Provides functionality for sending HTTP responses.
    /// </summary>
    public class ResponseSender
    {
        private readonly SecurityHeaders _securityHeaders;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="securityHeaders">The security headers every response carries, or null for the built-in defaults.</param>
        public ResponseSender(SecurityHeaders securityHeaders = null)
        {
            _securityHeaders = securityHeaders ?? SecurityHeaders.Default;
        }

        /// <summary>
        /// Sends the specified response message asynchronously to the provided context.
        /// </summary>
        /// <param name="context">The context of the request.</param>
        /// <param name="response">The reply message.</param>
        /// <param name="keepAlive">Indicates whether the connection should be kept alive after sending the response.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendAsync(IHttpContext context, IResponse response, bool keepAlive = false)
        {
            try
            {
                var responseFeature = context.Features.Get<IHttpResponseFeature>();
                var responseBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();

                if (responseFeature is null)
                {
                    // write error to server log
                    var log = WebEx.ComponentHub.LogManager.DefaultLog;
                    log.Error(context.RemoteEndPoint + ": The HTTP response feature is not available in the current context.");

                    return;
                }

                responseFeature.StatusCode = response.Status;
                responseFeature.ReasonPhrase = response.Reason;

                // Connection and Upgrade are connection-specific headers: they drive the HTTP/1.x
                // websocket handshake but are forbidden on HTTP/2+ (RFC 9113 §8.2.2), where Kestrel
                // rejects them. Only emit them while the connection still speaks HTTP/1.x.
                var allowConnectionSpecificHeaders = !IsHttp2OrHigher(context);
                var nonce = SecurityHeaders.CreateNonce();

                foreach (var header in _securityHeaders.Resolve(IsHttps(context), nonce))
                {
                    responseFeature.Headers[header.Key] = header.Value;
                }

                // a handler's own header wins over the server-wide default of the same name
                foreach (var header in response.Header.CustomHeader)
                {
                    responseFeature.Headers[header.Key] = header.Value;
                }

                if (!string.IsNullOrWhiteSpace(response.Header.ContentDisposition))
                {
                    responseFeature.Headers.ContentDisposition = response.Header.ContentDisposition;
                }

                if (!string.IsNullOrWhiteSpace(response.Header.ContentLanguage))
                {
                    responseFeature.Headers.ContentLanguage = response.Header.ContentLanguage;
                }

                if (response.Header.Location is not null)
                {
                    responseFeature.Headers.Location = response.Header.Location;
                }

                if (!string.IsNullOrWhiteSpace(response.Header.CacheControl))
                {
                    responseFeature.Headers.CacheControl = response.Header.CacheControl;
                }

                if (!string.IsNullOrWhiteSpace(response.Header.ContentType))
                {
                    responseFeature.Headers.ContentType = response.Header.ContentType;
                }
                else if (response.Content is IHtmlNode)
                {
                    // with nosniff the browser no longer guesses, and an untyped page would be shown as text
                    responseFeature.Headers.ContentType = $"text/html; charset={context.Encoding.WebName}";
                }

                if (response.Header.WWWAuthenticate)
                {
                    responseFeature.Headers.WWWAuthenticate = "Basic realm=\"Bereich\"";
                }

                if (response.Header.Cookies.Count != 0)
                {
                    // Cookie.ToString() only emits "name=value" and discards
                    // Path / Expires / Domain / SameSite - which makes the
                    // browser default-path the cookie to the request URI's
                    // directory. Build a proper Set-Cookie header per cookie
                    // so attributes survive the round trip.
                    var headerValues = response.Header.Cookies
                        .Cast<Cookie>()
                        .Select(cookie => SerializeSetCookie(cookie, response.Header.CookieSameSite.TryGetValue(cookie.Name, out var sameSite)
                            ? sameSite
                            : _securityHeaders.CookieSameSite))
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                    if (headerValues.Length > 0)
                    {
                        responseFeature.Headers.SetCookie = new StringValues(headerValues);
                    }
                }

                if (allowConnectionSpecificHeaders && !string.IsNullOrWhiteSpace(response.Header.Upgrade))
                {
                    responseFeature.Headers.Upgrade = response.Header.Upgrade;
                }

                if (allowConnectionSpecificHeaders && !string.IsNullOrWhiteSpace(response.Header.Connection))
                {
                    responseFeature.Headers.Connection = response.Header.Connection;
                }

                if (!string.IsNullOrWhiteSpace(response.Header.SecWebSocketAccept))
                {
                    responseFeature.Headers.SecWebSocketAccept = response.Header.SecWebSocketAccept;
                }

                if (response?.Content is byte[] byteContent)
                {
                    responseFeature.Headers.ContentLength = byteContent.Length;
                    await responseBodyFeature.Stream.WriteAsync(byteContent);
                    await responseBodyFeature.Stream.FlushAsync();
                }
                else if (response?.Content is string strContent)
                {
                    var content = context.Encoding.GetBytes(strContent);

                    responseFeature.Headers.ContentLength = content.Length;
                    await responseBodyFeature.Stream.WriteAsync(content);
                    await responseBodyFeature.Stream.FlushAsync();
                }
                else if (response?.Content is IHtmlNode htmlContent)
                {
                    string html;

                    using (HtmlScriptNonce.Begin(nonce))
                    {
                        html = htmlContent.ToString();
                    }

                    var content = context.Encoding.GetBytes(html);

                    responseFeature.Headers.ContentLength = content.Length;
                    await responseBodyFeature.Stream.WriteAsync(content);
                    await responseBodyFeature.Stream.FlushAsync();
                }

                if (!keepAlive)
                {
                    responseBodyFeature.Stream.Close();
                }
            }
            catch (Exception ex)
            {
                // write error to server log
                var log = WebEx.ComponentHub.LogManager.DefaultLog;
                log.Error(context.RemoteEndPoint + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Determines whether the negotiated protocol is HTTP/2 or higher, where connection-specific
        /// headers such as Connection and Upgrade are forbidden (RFC 9113 §8.2.2) and would be
        /// rejected by Kestrel. On HTTP/1.x these headers remain valid and carry the websocket
        /// handshake semantics.
        /// </summary>
        /// <param name="context">The request context whose negotiated protocol is inspected.</param>
        /// <returns>True when the protocol is HTTP/2 or higher; otherwise false.</returns>
        private static bool IsHttp2OrHigher(IHttpContext context)
        {
            // read straight from the request feature so the check does not depend on a fully
            // materialised request object (e.g. on the websocket or exception context paths)
            var protocol = context?.Features?.Get<IHttpRequestFeature>()?.Protocol;

            return protocol is not null
                && (protocol.StartsWith("HTTP/2", StringComparison.OrdinalIgnoreCase)
                    || protocol.StartsWith("HTTP/3", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the request arrived over https, the only transport on which a
        /// browser honours HSTS.
        /// </summary>
        /// <param name="context">The request context whose scheme is inspected.</param>
        /// <returns>True for an https request; otherwise false.</returns>
        private static bool IsHttps(IHttpContext context)
        {
            var scheme = context?.Features?.Get<IHttpRequestFeature>()?.Scheme;

            return string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Serialises a <see cref="Cookie"/> as a single Set-Cookie header
        /// value preserving Path, Domain, Expires and the HttpOnly / Secure
        /// flags. SameSite is passed in because System.Net.Cookie does not
        /// model the attribute.
        /// </summary>
        /// <param name="cookie">The cookie to serialise.</param>
        /// <param name="sameSite">The SameSite attribute of the cookie.</param>
        /// <returns>A Set-Cookie header value, or null when the cookie is empty.</returns>
        private static string SerializeSetCookie(Cookie cookie, SameSiteMode sameSite)
        {
            if (cookie is null || string.IsNullOrEmpty(cookie.Name))
            {
                return null;
            }

            var parts = new List<string>
            {
                $"{cookie.Name}={cookie.Value ?? string.Empty}"
            };

            if (!string.IsNullOrEmpty(cookie.Path))
            {
                parts.Add($"Path={cookie.Path}");
            }
            if (!string.IsNullOrEmpty(cookie.Domain))
            {
                parts.Add($"Domain={cookie.Domain}");
            }
            if (cookie.Expires != DateTime.MinValue)
            {
                // RFC 7231 IMF-fixdate: "Wed, 21 Oct 2015 07:28:00 GMT".
                parts.Add($"Expires={cookie.Expires.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)}");
            }
            if (cookie.HttpOnly)
            {
                parts.Add("HttpOnly");
            }
            if (cookie.Secure)
            {
                parts.Add("Secure");
            }

            if (sameSite != SameSiteMode.Unspecified)
            {
                parts.Add($"SameSite={sameSite}");
            }

            return string.Join("; ", parts);
        }
    }
}