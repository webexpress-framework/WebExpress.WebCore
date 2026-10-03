using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Optional overrides of the security headers and cookie attributes the server sends with
    /// every response. The defaults are restrictive on purpose: a deployment that omits the block
    /// is protected, and relaxing a protection is always an explicit, visible decision.
    /// </summary>
    public sealed class SecuritySettings
    {
        /// <summary>
        /// The content security policy. Left unset, a strict built-in policy applies that only
        /// allows scripts from the own origin plus the inline scripts the server itself renders,
        /// which carry a per-response nonce. The placeholder <c>{nonce}</c> in a custom policy is
        /// replaced by that nonce. An empty value switches the header off.
        /// </summary>
        public string ContentSecurityPolicy { get; set; }

        /// <summary>
        /// Sends the content security policy as <c>Content-Security-Policy-Report-Only</c>, so a
        /// deployment can observe violations in the browser console before enforcing the policy.
        /// </summary>
        public bool? ContentSecurityPolicyReportOnly { get; set; }

        /// <summary>
        /// The lifetime of the HSTS policy in seconds. It is sent on https responses only, since
        /// browsers ignore it over plain http. Left unset, one year applies; zero switches it off.
        /// </summary>
        public int? HstsMaxAge { get; set; }

        /// <summary>
        /// Extends HSTS to every subdomain. Off by default, because it also forces https on
        /// unrelated hosts below the same domain, which a deployment has to decide consciously.
        /// </summary>
        public bool? HstsIncludeSubDomains { get; set; }

        /// <summary>
        /// The SameSite attribute of cookies that do not demand a stricter or looser one
        /// themselves. Left unset, <c>Lax</c> applies: <c>Strict</c> would drop the session on
        /// every link that leads into the application from another site.
        /// </summary>
        public SameSiteMode? CookieSameSite { get; set; }

        /// <summary>
        /// Overrides further response headers by name. A value replaces the built-in default of
        /// that header, an empty value removes it, and an unknown name is added to every response.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Rejects state-changing requests and websocket connections another site triggers in
        /// the browser of a signed-in user. On by default; switching it off leaves cookie
        /// authentication open to cross-site request forgery.
        /// </summary>
        public bool? CsrfProtection { get; set; }

        /// <summary>
        /// Further origins, such as <c>https://portal.example.org</c>, that may send
        /// state-changing requests. Needed when a reverse proxy rewrites the host, so the
        /// browser's origin differs from the one the server sees.
        /// </summary>
        public List<string> TrustedOrigins { get; set; }

        /// <summary>
        /// The reverse proxies, as addresses such as <c>10.0.0.5</c> or ranges such as
        /// <c>172.16.0.0/12</c>, whose <c>X-Forwarded-For</c> header names the client. Behind a
        /// proxy every request arrives from the proxy's address, so per-client limits such as the
        /// login throttle would otherwise treat all users as one. The header is honoured only on
        /// requests that come from one of these proxies, since any other client could use it to
        /// pick the address it is limited under. Left unset, the connection's own address is used.
        /// </summary>
        public List<string> TrustedProxies { get; set; }

        /// <summary>
        /// Shows the exception behind an internal server error - its message, source and stack
        /// trace - on the error page. Off by default, because those details name types, paths and
        /// configuration values an anonymous caller must not see; the log records them either way.
        /// Meant for development only.
        /// </summary>
        public bool? DetailedErrors { get; set; }
    }
}
