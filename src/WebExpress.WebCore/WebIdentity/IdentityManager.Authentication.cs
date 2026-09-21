using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Connects credential issuance and request verification without retaining an identity in session state.
    /// </summary>
    public partial class IdentityManager
    {
        /// <summary>
        /// Prevents insecure origins and sibling domains from planting an authentication cookie.
        /// </summary>
        public const string AccessCookieName = "__Host-wx-access";

        /// <summary>
        /// Allows a narrow cookie path while retaining the browser's Secure-prefix protection.
        /// </summary>
        public const string RefreshCookieName = "__Secure-wx-refresh";
        internal const string DevelopmentAccessCookieName = "wx-access";
        internal const string DevelopmentRefreshCookieName = "wx-refresh";

        /// <summary>
        /// Keeps secure transport mandatory unless the deployment explicitly opts into development HTTP.
        /// </summary>
        internal bool RequiresHttps => _httpServerContext.Configuration.GetValue("WebExpress:Authentication:RequireHttps", true);

        /// <summary>
        /// Separates development cookies from the browser-enforced production cookie namespace.
        /// </summary>
        private string AccessCookie => RequiresHttps ? AccessCookieName : DevelopmentAccessCookieName;

        /// <summary>
        /// Keeps development renewal credentials outside the protected production cookie namespace.
        /// </summary>
        private string RefreshCookie => RequiresHttps ? RefreshCookieName : DevelopmentRefreshCookieName;
        /// <summary>
        /// Keeps refresh credentials out of ordinary application requests.
        /// </summary>
        public const string RefreshPath = "/api/auth/refresh";

        private readonly ConditionalWeakTable<IRequest, AuthenticationState> _authenticationStates = new();
        private readonly object _tokenGate = new();
        private IdentityTokenService _tokens;

        /// <summary>
        /// Keeps pending authentication changes local to one HTTP request.
        /// </summary>
        private sealed class AuthenticationState
        {
            internal IIdentity Identity;
            internal IdentityTokenPair Pair;
            internal bool Changed;
        }

        /// <summary>
        /// Gets the token service, creating it if necessary.
        /// </summary>
        internal IdentityTokenService Tokens
        {
            get
            {
                lock (_tokenGate)
                {
                    if (_tokens is not null) { return _tokens; }
                    var settings = _httpServerContext.Configuration.GetSection("WebExpress:Authentication").Get<AuthenticationSettings>();
                    if (settings is null) { return null; }
                    _tokens = new IdentityTokenService(settings, new FileIdentityTokenStore(settings.TokenStorePath));
                    return _tokens;
                }
            }
        }

        /// <summary>
        /// Converts a verified identity into a credential-free snapshot and queues protected token cookies.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>The issued token pair, or null when no identity was authenticated.</returns>
        public IdentityTokenPair Login(IIdentity identity, IRequest request)
        {
            if (identity is null) { return null; }
            ArgumentNullException.ThrowIfNull(request);
            var snapshot = Snapshot(identity, request.ApplicationContext);
            var pair = RequireTokens().Issue(snapshot, request.ApplicationContext.ApplicationId);
            var state = _authenticationStates.GetOrCreateValue(request);
            state.Identity = snapshot;
            state.Pair = pair;
            state.Changed = true;
            return pair;
        }

        /// <summary>
        /// Rotates the refresh credential without extending the original grant or creating a session.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>The rotated token pair, or null when validation or replay protection rejects renewal.</returns>
        public IdentityTokenPair Refresh(IRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var pair = RequireTokens().Refresh(CookieValue(request, RefreshCookie), request.ApplicationContext?.ApplicationId);
            if (pair is null) { return null; }
            var state = _authenticationStates.GetOrCreateValue(request);
            state.Identity = Tokens.ValidateAccessToken(pair.AccessToken, request.ApplicationContext.ApplicationId);
            state.Pair = pair;
            state.Changed = true;
            return pair;
        }

        /// <summary>
        /// Clears browser credentials and revokes future renewal while issued access tokens expire naturally.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        public void Logout(IRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            Tokens?.RevokeGrant(CookieValue(request, AccessCookie), request.ApplicationContext?.ApplicationId);
            Tokens?.RevokeRefreshGrant(CookieValue(request, RefreshCookie), request.ApplicationContext?.ApplicationId);
            var state = _authenticationStates.GetOrCreateValue(request);
            state.Identity = null;
            state.Pair = null;
            state.Changed = true;
        }

        /// <summary>
        /// Authenticates each request from a signed cookie or an explicitly supplied personal bearer credential.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>The verified request identity, or null when no accepted credential authenticates it.</returns>
        public IIdentity GetCurrentIdentity(IRequest request)
        {
            if (request?.ApplicationContext is null) { return null; }
            if (_authenticationStates.TryGetValue(request, out var state)) { return state.Identity; }
            var authorization = request.Header.Authorization;
            // an explicit, invalid authorization header must never fall back to browser credentials
            if (authorization is not null)
            {
                return string.Equals(authorization.Type, "Bearer", StringComparison.OrdinalIgnoreCase)
                    ? Tokens?.ValidatePersonalAccessToken(authorization.Token, request.ApplicationContext.ApplicationId) : null;
            }
            var cookie = CookieValue(request, AccessCookie);
            return cookie is null ? null : Tokens?.ValidateAccessToken(cookie, request.ApplicationContext.ApplicationId);
        }

        /// <summary>
        /// Creates an explicitly bounded credential whose permissions cannot exceed the owning identity.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationContext">The application context that owns the requested operation.</param>
        /// <param name="lifetime">The explicit validity period requested for the personal credential.</param>
        /// <param name="permissions">The requested permission identifiers, limited to the owner's grants.</param>
        /// <returns>The signed personal credential with the requested authorized permissions.</returns>
        public string CreatePersonalAccessToken(IIdentity identity, IApplicationContext applicationContext,
            TimeSpan lifetime, IEnumerable<string> permissions)
        {
            return RequireTokens().CreatePersonalAccessToken(Snapshot(identity, applicationContext),
                applicationContext.ApplicationId, lifetime, permissions);
        }

        /// <summary>
        /// Persists revocation so the personal credential stops working across all configured instances.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application context that owns the requested operation.</param>
        /// <returns>True when a valid personal credential was revoked; otherwise, false.</returns>
        public bool RevokePersonalAccessToken(string token, IApplicationContext applicationContext)
        {
            return RequireTokens().RevokePersonalAccessToken(token, applicationContext.ApplicationId);
        }

        /// <summary>
        /// Applies queued credential changes to the outgoing response while keeping tokens out of its body.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="response">The outgoing response governed by the authentication transport contract.</param>
        public void ApplyAuthenticationCookies(IRequest request, IResponse response)
        {
            if (request is null || response is null || !_authenticationStates.TryGetValue(request, out var state) || !state.Changed) { return; }
            response.Header.CacheControl = "no-store";
            // an expired access credential still identifies the grant for logout; validation always enforces its signed expiry
            response.Header.Cookies.Add(CreateCookie(AccessCookie, state.Pair?.AccessToken, "/", state.Pair?.RefreshTokenExpiresAt, RequiresHttps));
            response.Header.Cookies.Add(CreateCookie(RefreshCookie, state.Pair?.RefreshToken, RefreshPath, state.Pair?.RefreshTokenExpiresAt, RequiresHttps));
        }

        /// <summary>
        /// Applies protected cookie attributes and an expired deadline when removing a credential.
        /// </summary>
        /// <param name="name">The exact cookie or claim name to inspect.</param>
        /// <param name="value">The value being validated or placed in the protected response.</param>
        /// <param name="path">The route or cookie path that constrains credential use.</param>
        /// <param name="expiration">The cookie deadline, or null when the cookie must be deleted.</param>
        /// <param name="secure">Whether the browser must restrict the cookie to HTTPS transport.</param>
        /// <returns>The protected authentication cookie or its deletion counterpart.</returns>
        internal static Cookie CreateCookie(string name, string value, string path, DateTimeOffset? expiration, bool secure = true)
        {
            return new Cookie(name, value ?? "", path)
            {
                Secure = secure,
                HttpOnly = true,
                Expires = expiration?.UtcDateTime ?? DateTime.UnixEpoch
            };
        }

        /// <summary>
        /// Rejects duplicate credentials so browser cookie ordering cannot determine the authenticated identity.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="name">The exact cookie or claim name to inspect.</param>
        /// <returns>The matching cookie value, or null when absent or ambiguous.</returns>
        internal static string CookieValue(IRequest request, string name)
        {
            var cookies = request.Header.Cookies.Where(x => x.Name == name).ToArray();
            return cookies.Length == 1 ? cookies[0].Value : null;
        }

        /// <summary>
        /// Prevents authentication from silently falling back to ephemeral signing configuration.
        /// </summary>
        /// <returns>The token service configured for this deployment.</returns>
        private IdentityTokenService RequireTokens() => Tokens ?? throw new InvalidOperationException("Configure WebExpress:Authentication before signing in.");

        /// <summary>
        /// Captures effective authorization before mutable provider state crosses the token boundary.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="application">The application whose provider bindings or authorization definitions apply.</param>
        /// <returns>The immutable identity containing the effective authorization claims.</returns>
        private Identity Snapshot(IIdentity identity, IApplicationContext application)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(application);
            var permissions = identity.Permissions.Concat(Permissions.Where(x => x.ApplicationContext == application &&
                (CheckAccess(application, identity, x.Permission) ||
                 identity.PolicyNames.Any(policy => CheckAccess(application, policy, x.Permission))))
                .Select(x => x.Permission.FullName));
            return new Identity(identity.Id, identity.Name, identity.Email, identity.Roles, permissions, identity.PolicyNames);
        }
    }
}
