using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using System;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Provides one credential interface for local and external identities before normal application routing.
    /// Unsafe browser operations require a same-origin custom header and never accept credentials in URLs.
    /// </summary>
    public sealed class AuthenticationEndpoint : IDisposable
    {
        private readonly IComponentHub _hub;
        private readonly IHttpServerContext _server;
        private readonly ForwardedClientResolver _clientResolver;
        private readonly PartitionedRateLimiter<string> _loginLimiter = PartitionedRateLimiter.Create<string, string>(
            key => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10, Window = TimeSpan.FromMinutes(1), AutoReplenishment = true, QueueLimit = 0
            }));

        /// <summary>
        /// Connects endpoint handling to the same manager used by application login forms.
        /// </summary>
        /// <param name="componentHub">The component hub that supplies application-scoped authentication services.</param>
        /// <param name="httpServerContext">The server context that supplies deployment configuration and framework services.</param>
        /// <param name="clientResolver">
        /// Names the client behind trusted proxies, so the login throttle limits each client instead of
        /// the proxy; null limits by the connection's own address.
        /// </param>
        public AuthenticationEndpoint(IComponentHub componentHub, IHttpServerContext httpServerContext, ForwardedClientResolver clientResolver = null)
        {
            _hub = componentHub;
            _server = httpServerContext;
            _clientResolver = clientResolver ?? new ForwardedClientResolver(null);
        }

        /// <summary>
        /// Returns null for ordinary routes so the server's normal routing remains authoritative.
        /// Authentication errors receive stable responses that contain neither passwords nor tokens.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>The authentication response, or null when normal application routing should handle the request.</returns>
        public async Task<IResponse> HandleAsync(IRequest request)
        {
            var path = "/" + string.Join("/", request.Uri.PathSegments.Where(x => x is not UriPathSegmentRoot).Select(x => x.Value));
            if (!path.StartsWith("/api/auth/", StringComparison.Ordinal)) { return null; }
            var response = await HandleCoreAsync(request, path);
            response.Header.CacheControl = "no-store";
            response.Header.CustomHeader["Pragma"] = "no-cache";
            response.Header.CustomHeader["Referrer-Policy"] = "no-referrer";
            if (path == "/api/auth/callback")
            {
                response.Header.Cookies.Add(IdentityManager.CreateCookie(OpenIdConnectIdentityProvider.ChallengeCookieName,
                    null, "/api/auth/callback", null));
            }
            _hub.IdentityManager.ApplyAuthenticationCookies(request, response);
            return response;
        }

        /// <summary>
        /// Enforces transport and browser trust boundaries before dispatching authentication operations.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="path">The request route or cookie path that constrains credential use.</param>
        /// <returns>The response for the selected authentication operation.</returns>
        private async Task<IResponse> HandleCoreAsync(IRequest request, string path)
        {
            var manager = (IdentityManager)_hub.IdentityManager;
            if (manager.RequiresHttps && request.Scheme != UriScheme.Https) { return Error(new ResponseBadRequest(), "https_required"); }
            var expectedMethod = path is "/api/auth/authorize" or "/api/auth/callback" ? RequestMethod.GET : RequestMethod.POST;
            var isDelete = request.Method == RequestMethod.DELETE && path is IdentityManager.RefreshPath or "/api/auth/pat";
            if (request.Method != expectedMethod && !isDelete)
            {
                var method = Error(new ResponseMethodNotAllowed(), "method_not_allowed");
                method.Header.CustomHeader["Allow"] = expectedMethod.ToString();
                return method;
            }
            if (request.Method != RequestMethod.GET &&
                (request.Header.AuthenticationRequest != "1" || !SameOrigin(request)))
            {
                return Error(new ResponseForbidden(), "invalid_origin");
            }
            var applicationId = OpenIdConnectIdentityProvider.Query(request, "application") ??
                _server.Configuration["WebExpress:Authentication:ApplicationId"];
            var application = _hub.ApplicationManager.Applications.SingleOrDefault(x => x.ApplicationId == applicationId);
            if (application is null || request is not RequestBase concrete) { return Error(new ResponseBadRequest(), "unknown_application"); }
            concrete.ApplicationContext = application;
            if (!manager.IsAuthenticationConfigured(application)) { return Error(new ResponseServiceUnavailable(), "authentication_not_configured"); }

            try
            {
                switch (path)
                {
                    case "/api/auth/login":
                        using (var lease = _loginLimiter.AttemptAcquire(_clientResolver.Resolve(request)?.ToString() ?? "unknown"))
                        {
                            if (!lease.IsAcquired) { manager.Metrics?.Logins.Increment("throttled"); return Error(new ResponseTooManyRequests(), "try_again_later"); }
                            using var body = ReadBody(request);
                            var username = body.RootElement.GetProperty("username").GetString();
                            var password = body.RootElement.GetProperty("password").GetString();
                            var rememberMe = !body.RootElement.TryGetProperty("rememberMe", out var persistence) || persistence.GetBoolean();
                            var identity = _hub.IdentityProviderManager.GetProviders(application).OfType<LocalIdentityProvider>()
                                .Select(x => x.Authenticate(username, password)).FirstOrDefault(x => x is not null);
                            if (identity is null) { manager.Metrics?.Logins.Increment("failure"); return Unauthorized(); }
                            return SignedIn(manager.Login(identity, request, rememberMe));
                        }
                    case IdentityManager.RefreshPath:
                        if (isDelete) { manager.Logout(request); return new ResponseNoContent(); }
                        var pair = manager.Refresh(request);
                        return pair is null ? Unauthorized() : SignedIn(pair);
                    case "/api/auth/logout":
                        manager.Logout(request);
                        return new ResponseNoContent();
                    case "/api/auth/authorize":
                    case "/api/auth/callback":
                        return await HandleOpenIdConnectAsync(request, manager, path == "/api/auth/authorize");
                    case "/api/auth/pat":
                        var owner = manager.GetCurrentIdentity(request);
                        if (owner is null || request.Header.Authorization is not null) { return Unauthorized(); }
                        using (var body = ReadBody(request))
                        {
                            if (isDelete)
                            {
                                var token = body.RootElement.GetProperty("token").GetString();
                                if (manager.ValidatePersonalAccessToken(token, application)?.Id != owner.Id) { return Unauthorized(); }
                                manager.RevokePersonalAccessToken(token, application);
                                return new ResponseNoContent();
                            }
                            var permissions = body.RootElement.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ToArray();
                            var lifetime = body.RootElement.GetProperty("lifetimeSeconds").GetInt32();
                            var personal = manager.CreatePersonalAccessToken(owner, application, TimeSpan.FromSeconds(lifetime), permissions);
                            return Json(new ResponseCreated(), new { token = personal });
                        }
                    default:
                        return Error(new ResponseNotFound(), "unknown_authentication_endpoint");
                }
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or
                System.Collections.Generic.KeyNotFoundException or FormatException)
            {
                return Error(new ResponseBadRequest(), "invalid_authentication_request");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return Error(new ResponseServiceUnavailable(), "identity_provider_unavailable");
            }
        }

        /// <summary>
        /// Completes browser navigation with destinations captured before leaving the application.
        /// Requests without a browser destination retain the JSON authentication contract.
        /// </summary>
        /// <param name="request">The authorization or callback request scoped to a known application.</param>
        /// <param name="manager">The identity manager that validates correlation and issues local cookies.</param>
        /// <param name="authorize">Whether this request starts a new authorization flow.</param>
        /// <returns>The provider redirect, local browser redirect, or JSON authentication response.</returns>
        private async Task<IResponse> HandleOpenIdConnectAsync(IRequest request, IdentityManager manager, bool authorize)
        {
            var providerId = OpenIdConnectIdentityProvider.Query(request, "provider");
            string returnUrl = null;
            string failureUrl = null;
            var rememberMe = true;
            if (authorize)
            {
                var persistence = OpenIdConnectIdentityProvider.Query(request, "rememberMe");
                if (persistence is not null && !bool.TryParse(persistence, out rememberMe))
                {
                    return Error(new ResponseBadRequest(), "invalid_authentication_request");
                }
                returnUrl = OpenIdConnectIdentityProvider.Query(request, "returnUrl");
                failureUrl = OpenIdConnectIdentityProvider.Query(request, "failureUrl") ?? returnUrl;
                if ((returnUrl is not null && !OpenIdConnectIdentityProvider.IsLocalRedirectUri(returnUrl)) ||
                    (failureUrl is not null && (returnUrl is null || !OpenIdConnectIdentityProvider.IsLocalRedirectUri(failureUrl))))
                {
                    return Error(new ResponseBadRequest(), "invalid_redirect");
                }
            }
            else
            {
                // callback parameters cannot replace destinations from the initiating browser
                var challenge = manager.ValidateChallenge(IdentityManager.CookieValue(request,
                    OpenIdConnectIdentityProvider.ChallengeCookieName), request.ApplicationContext);
                rememberMe = challenge is not null &&
                    (!challenge.TryGetPayloadValue<bool>("remember_me", out var persistent) || persistent);
                if (challenge is not null && challenge.TryGetPayloadValue<string>("provider", out var boundProvider) &&
                    boundProvider == providerId && challenge.TryGetPayloadValue<string>("returnUrl", out var destination) &&
                    challenge.TryGetPayloadValue<string>("failureUrl", out var failure) &&
                    OpenIdConnectIdentityProvider.IsLocalRedirectUri(destination) && OpenIdConnectIdentityProvider.IsLocalRedirectUri(failure))
                {
                    returnUrl = destination;
                    failureUrl = failure;
                }
            }

            try
            {
                var providers = _hub.IdentityProviderManager.GetProviders(request.ApplicationContext)
                    .OfType<OpenIdConnectIdentityProvider>().Where(x => x.ProviderId == providerId).ToArray();
                if (providers.Length != 1)
                {
                    return BrowserFailure(failureUrl, new ResponseBadRequest(), "unknown_provider");
                }
                if (authorize)
                {
                    return await providers[0].CreateChallengeAsync(manager, request.ApplicationContext, returnUrl, failureUrl, rememberMe);
                }
                var external = await providers[0].AuthenticateCallbackAsync(request, manager);
                if (external is null)
                {
                    manager.Metrics?.Logins.Increment("failure");
                    return BrowserFailure(failureUrl, new ResponseUnauthorized(), "authentication_failed");
                }
                var pair = manager.Login(external, request, rememberMe);
                return returnUrl is null ? SignedIn(pair) : BrowserRedirect(returnUrl);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return BrowserFailure(failureUrl, new ResponseServiceUnavailable(), "identity_provider_unavailable");
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or
                System.Collections.Generic.KeyNotFoundException or FormatException)
            {
                return BrowserFailure(failureUrl, new ResponseBadRequest(), "invalid_authentication_request");
            }
        }

        /// <summary>
        /// Returns stable error codes to a trusted login page without reflecting provider error descriptions.
        /// </summary>
        /// <param name="failureUrl">The validated local login destination, or null for a JSON caller.</param>
        /// <param name="response">The status response used when no browser destination is available.</param>
        /// <param name="error">The framework error code suitable for translation by a login control.</param>
        /// <returns>A local failure redirect or the JSON error response.</returns>
        private static IResponse BrowserFailure(string failureUrl, IResponse response, string error)
        {
            response.Header.WWWAuthenticate = false;
            return failureUrl is null ? Error(response, error) :
                BrowserRedirect(QueryHelpers.AddQueryString(failureUrl, "wx-auth-error", error));
        }

        /// <summary>
        /// Leaves authentication endpoints without exposing tokens in the destination or response body.
        /// </summary>
        /// <param name="destination">The previously validated local destination.</param>
        /// <returns>A temporary redirect to the application.</returns>
        private static IResponse BrowserRedirect(string destination)
        {
            var response = new ResponseMovedTemporarily();
            response.Header.Location = destination;
            return response;
        }

        /// <summary>
        /// Bounds credential input and accepts only explicitly declared JSON payloads.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>The parsed and size-bounded JSON document owned by the caller.</returns>
        private static JsonDocument ReadBody(IRequest request)
        {
            if (request is not Request concrete || concrete.Content is null || concrete.Content.Length > 16384 ||
                !string.Equals(request.Header.ContentType?.Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("An application/json body is required.");
            }
            return JsonDocument.Parse(concrete.Content);
        }

        /// <summary>
        /// Rejects browser origins that could otherwise mutate another site's authentication cookies.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>True when no browser origin is supplied or the origin matches the request; otherwise, false.</returns>
        private static bool SameOrigin(IRequest request)
        {
            if (string.IsNullOrEmpty(request.Header.Origin)) { return true; }
            return Uri.TryCreate(request.Header.Origin, UriKind.Absolute, out var origin) &&
                Uri.TryCreate(request.Uri.ToString(), UriKind.Absolute, out var target) &&
                origin.GetLeftPart(UriPartial.Authority) == target.GetLeftPart(UriPartial.Authority);
        }

        /// <summary>
        /// Exposes authentication status while keeping both browser credentials in protected cookies.
        /// </summary>
        /// <param name="pair">The issued credentials whose public expiration metadata is returned.</param>
        /// <returns>The JSON response containing status and expiration metadata only.</returns>
        private static IResponse SignedIn(IdentityTokenPair pair) => Json(new ResponseOK(), new { authenticated = true, expiresAt = pair.AccessTokenExpiresAt });
        /// <summary>
        /// Uses a uniform failure response that does not disclose account or provider details.
        /// </summary>
        /// <returns>The uniform unauthorized response without a Basic authentication challenge.</returns>
        private static IResponse Unauthorized()
        {
            var response = new ResponseUnauthorized();
            response.Header.WWWAuthenticate = false;
            return Error(response, "authentication_failed");
        }
        /// <summary>
        /// Keeps authentication failures machine-readable without exposing internal exceptions.
        /// </summary>
        /// <param name="response">The outgoing response that must obey the authentication transport contract.</param>
        /// <param name="error">The stable error identifier exposed to the client.</param>
        /// <returns>The response carrying the stable JSON error contract.</returns>
        private static IResponse Error(IResponse response, string error) => Json(response, new { error });
        /// <summary>
        /// Applies a consistent JSON content type and serialization contract to authentication responses.
        /// </summary>
        /// <param name="response">The outgoing response that must obey the authentication transport contract.</param>
        /// <param name="body">The payload serialized or supplied for the authentication request.</param>
        /// <returns>The response containing the serialized JSON payload.</returns>
        private static IResponse Json(IResponse response, object body)
        {
            response.Header.ContentType = "application/json; charset=utf-8";
            response.Content = JsonSerializer.Serialize(body);
            return response;
        }

        /// <summary>
        /// Releases limiter timers when the hosting server shuts down.
        /// </summary>
        public void Dispose() => _loginLimiter.Dispose();
    }
}
