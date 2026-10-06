using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Protects cookie-authenticated requests against cross-site request forgery by checking
    /// where a request comes from, as recommended by OWASP for fetch metadata and origin
    /// verification. It needs no token in forms or scripts: browsers attach both headers on
    /// their own and a page of another site cannot suppress or forge them.
    /// </summary>
    /// <remarks>
    /// A request carrying neither header does not come from a current browser, and so cannot
    /// have been forged within one; command-line and server-side clients keep working.
    /// </remarks>
    public sealed class RequestOriginGuard
    {
        private readonly bool _enabled;
        private readonly HashSet<string> _trustedOrigins;

        /// <summary>
        /// Gets whether cross-site requests are rejected.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Gets the further origins that may send state-changing requests.
        /// </summary>
        public IEnumerable<string> TrustedOrigins => _trustedOrigins.Order(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="settings">The configured overrides, or null for the built-in defaults.</param>
        public RequestOriginGuard(SecuritySettings settings)
        {
            _enabled = settings?.CsrfProtection ?? true;
            _trustedOrigins = new HashSet<string>
            (
                (settings?.TrustedOrigins ?? []).Select(Normalize).Where(x => x is not null),
                StringComparer.OrdinalIgnoreCase
            );
        }

        /// <summary>
        /// Determines whether the request may be processed.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <param name="webSocket">Whether the request opens a websocket, which browsers allow across sites and which therefore needs the check despite being a GET.</param>
        /// <returns>True when the request is allowed; false when another site triggered it.</returns>
        public bool IsAllowed(IRequest request, bool webSocket = false)
        {
            if (!_enabled || request is null)
            {
                return true;
            }

            if (!webSocket && request.Method is RequestMethod.GET or RequestMethod.HEAD)
            {
                return true;
            }

            // an explicit credential is never sent automatically, so a foreign page cannot borrow it
            if (!webSocket && request.Header?.Authorization is not null)
            {
                return true;
            }

            var origin = Normalize(request.Header?.Origin);

            if (origin is not null && (_trustedOrigins.Contains(origin) || origin == OwnOrigin(request)))
            {
                return true;
            }

            // the browser's own verdict also holds behind a proxy that rewrites the host
            return request.Header?.SecFetchSite?.ToLowerInvariant() switch
            {
                "same-origin" or "none" => true,
                null => string.IsNullOrEmpty(request.Header?.Origin),
                _ => false
            };
        }

        /// <summary>
        /// Determines the origin the browser addressed. It is taken from the host header rather
        /// than the request uri, which carries the local listening port and would differ from
        /// the browser's view whenever a container or proxy maps the port.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The origin, or null when it cannot be determined.</returns>
        private static string OwnOrigin(IRequest request)
        {
            var scheme = Uri.TryCreate(request.Uri?.ToString(), UriKind.Absolute, out var uri) ? uri.Scheme : null;

            return scheme is null || string.IsNullOrWhiteSpace(request.Header?.Host)
                ? null
                : Normalize($"{scheme}://{request.Header.Host}");
        }

        /// <summary>
        /// Reduces a url to its scheme, host and port, the part an origin consists of.
        /// </summary>
        /// <param name="url">The url or origin.</param>
        /// <returns>The origin, or null when the value is empty, opaque ("null") or not a url.</returns>
        private static string Normalize(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                ? uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant()
                : null;
        }
    }
}
