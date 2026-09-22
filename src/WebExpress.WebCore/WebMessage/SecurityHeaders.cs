using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Resolves the security headers every response carries. The defaults follow the OWASP secure
    /// headers recommendations, so an application is protected without configuring anything;
    /// <see cref="SecuritySettings"/> can only loosen them explicitly.
    /// </summary>
    public sealed class SecurityHeaders
    {
        /// <summary>
        /// The policy applied when a server runs without a security settings block.
        /// </summary>
        public static SecurityHeaders Default { get; } = new(null);

        /// <summary>
        /// The built-in content security policy. Scripts are the actual attack surface and are
        /// limited to the own origin and the nonce-carrying inline scripts the server renders.
        /// Inline styles stay allowed because the controls emit style attributes throughout,
        /// and style injection cannot execute code. Images and media may come from any https
        /// source, since editor content legitimately references external pictures.
        /// </summary>
        public const string DefaultContentSecurityPolicy =
            "default-src 'self'; " +
            "script-src 'self' 'nonce-{nonce}'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob: https:; " +
            "media-src 'self' blob: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'self'";

        private const int DefaultHstsMaxAge = 365 * 24 * 60 * 60;

        private readonly string _contentSecurityPolicy;
        private readonly string _contentSecurityPolicyHeader;
        private readonly string _hsts;
        private readonly Dictionary<string, string> _headers;

        /// <summary>
        /// Gets the SameSite attribute of cookies that do not set their own.
        /// </summary>
        public SameSiteMode CookieSameSite { get; }

        /// <summary>
        /// Gets the content security policy with the <c>{nonce}</c> placeholder still in place,
        /// or null when the header is switched off.
        /// </summary>
        public string ContentSecurityPolicy => string.IsNullOrWhiteSpace(_contentSecurityPolicy) ? null : _contentSecurityPolicy;

        /// <summary>
        /// Gets whether the policy is only reported instead of enforced.
        /// </summary>
        public bool ContentSecurityPolicyReportOnly => _contentSecurityPolicyHeader != "Content-Security-Policy";

        /// <summary>
        /// Gets the HSTS value sent on https responses, or null when HSTS is switched off.
        /// </summary>
        public string StrictTransportSecurity => _hsts;

        /// <summary>
        /// Gets the further headers every response carries, without the switched-off ones.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers => _headers
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="settings">The configured overrides, or null for the built-in defaults.</param>
        public SecurityHeaders(SecuritySettings settings)
        {
            _contentSecurityPolicy = settings?.ContentSecurityPolicy ?? DefaultContentSecurityPolicy;
            _contentSecurityPolicyHeader = settings?.ContentSecurityPolicyReportOnly == true
                ? "Content-Security-Policy-Report-Only"
                : "Content-Security-Policy";

            var maxAge = settings?.HstsMaxAge ?? DefaultHstsMaxAge;
            _hsts = maxAge > 0
                ? $"max-age={maxAge}" + (settings?.HstsIncludeSubDomains == true ? "; includeSubDomains" : "")
                : null;

            // unspecified is not a valid attribute value, so it falls back to the default
            CookieSameSite = settings?.CookieSameSite is SameSiteMode mode && mode != SameSiteMode.Unspecified
                ? mode
                : SameSiteMode.Lax;

            _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Content-Type-Options"] = "nosniff",
                // frame-ancestors supersedes it, but older browsers only know this header
                ["X-Frame-Options"] = "SAMEORIGIN",
                ["Referrer-Policy"] = "strict-origin-when-cross-origin",
                ["Cross-Origin-Opener-Policy"] = "same-origin",
                ["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()"
            };

            foreach (var header in settings?.Headers ?? [])
            {
                _headers[header.Key] = header.Value;
            }
        }

        /// <summary>
        /// Creates a nonce for one response. 128 random bits make guessing it infeasible, which
        /// is all the content security policy relies on.
        /// </summary>
        /// <returns>The nonce, base64 encoded.</returns>
        public static string CreateNonce()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        }

        /// <summary>
        /// Resolves the headers of one response.
        /// </summary>
        /// <param name="https">Whether the response travels over https; HSTS is only valid there.</param>
        /// <param name="nonce">The nonce the inline scripts of the response carry.</param>
        /// <returns>The header names and values, without the switched-off ones.</returns>
        public IEnumerable<KeyValuePair<string, string>> Resolve(bool https, string nonce)
        {
            if (!string.IsNullOrWhiteSpace(_contentSecurityPolicy))
            {
                yield return new(_contentSecurityPolicyHeader, _contentSecurityPolicy.Replace("{nonce}", nonce));
            }

            if (https && _hsts is not null)
            {
                yield return new("Strict-Transport-Security", _hsts);
            }

            foreach (var header in _headers)
            {
                if (!string.IsNullOrWhiteSpace(header.Value))
                {
                    yield return header;
                }
            }
        }
    }
}
