using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Completes authorization-code authentication before translating verified external claims into
    /// WebExpress identities. State, nonce, and PKCE are bound to a signed HTTP-only browser cookie.
    /// </summary>
    public abstract class OpenIdConnectIdentityProvider : IIdentityProvider, IDisposable
    {
        internal const string ChallengeCookieName = "__Secure-wx-oidc";
        private readonly OpenIdConnectSettings _settings;
        private readonly HttpClient _client;
        private readonly bool _ownsClient;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuration;
        private readonly JsonWebTokenHandler _handler = new() { MaximumTokenSizeInBytes = 16384 };

        /// <summary>
        /// Exposes the configured selection key without exposing client secrets to callers.
        /// </summary>
        public string ProviderId => _settings.ProviderId;

        /// <summary>
        /// Pins HTTPS endpoints and uses cached discovery with signing-key refresh for key rotation.
        /// An injected client permits controlled backchannel transport and deterministic integration tests.
        /// </summary>
        /// <param name="settings">The authority, client credentials, redirect URI, and local authorization mappings.</param>
        /// <param name="client">The optional HTTP transport used for discovery and code exchange.</param>
        protected OpenIdConnectIdentityProvider(OpenIdConnectSettings settings, HttpClient client = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ProviderId);
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ClientId);
            RequireHttps(settings.Authority);
            RequireHttps(settings.RedirectUri);
            var redirect = new Uri(settings.RedirectUri);
            if (redirect.AbsolutePath != "/api/auth/callback" || !string.IsNullOrEmpty(redirect.Fragment))
            {
                throw new ArgumentException("The OIDC redirect must use /api/auth/callback without a fragment.");
            }
            _settings = new OpenIdConnectSettings
            {
                ProviderId = settings.ProviderId, Authority = settings.Authority, ClientId = settings.ClientId,
                ClientSecret = settings.ClientSecret, RedirectUri = settings.RedirectUri,
                RolePermissions = settings.RolePermissions.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal),
                RolePolicies = settings.RolePolicies.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal)
            };
            _ownsClient = client is null;
            _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
            _configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
                settings.Authority.TrimEnd('/') + "/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever(_client) { RequireHttps = true });
        }

        /// <summary>
        /// Starts a code flow whose verifier remains in the initiating browser's protected cookie.
        /// </summary>
        /// <param name="tokens">The shared token service used to bind browser correlation to the local signing authority.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>A redirect response with a protected browser correlation cookie.</returns>
        public async Task<IResponse> CreateChallengeAsync(IdentityTokenService tokens, string applicationId)
        {
            var redirectQuery = QueryHelpers.ParseQuery(new Uri(_settings.RedirectUri).Query);
            if (redirectQuery["application"].Count != 1 || redirectQuery["application"] != applicationId ||
                redirectQuery["provider"].Count != 1 || redirectQuery["provider"] != ProviderId)
            {
                throw new InvalidOperationException("The registered callback must identify its application and provider.");
            }
            var configuration = await GetConfigurationAsync();
            var state = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var challenge = tokens.ProtectChallenge(new Dictionary<string, object>
            {
                ["state"] = state, ["nonce"] = nonce, ["verifier"] = verifier, ["provider"] = ProviderId
            }, applicationId);
            var response = new ResponseMovedTemporarily();
            response.Header.Location = QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId, ["redirect_uri"] = _settings.RedirectUri,
                ["response_type"] = "code", ["scope"] = "openid profile email", ["response_mode"] = "query",
                ["state"] = state, ["nonce"] = nonce, ["code_challenge_method"] = "S256",
                ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            });
            response.Header.Cookies.Add(IdentityManager.CreateCookie(ChallengeCookieName, challenge,
                "/api/auth/callback", DateTimeOffset.UtcNow.AddMinutes(5)));
            response.Header.CacheControl = "no-store";
            return response;
        }

        /// <summary>
        /// Accepts only a browser-correlated authorization code and validates the resulting ID token's
        /// signature, issuer, audience, lifetime, authorized party, and nonce before mapping claims.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="tokens">The shared token service used to bind browser correlation to the local signing authority.</param>
        /// <returns>The normalized external identity, or null when correlation or token validation fails.</returns>
        public async Task<IIdentity> AuthenticateCallbackAsync(IRequest request, IdentityTokenService tokens)
        {
            var challenge = tokens.ValidateChallenge(IdentityManager.CookieValue(request, ChallengeCookieName), request.ApplicationContext?.ApplicationId);
            var state = Query(request, "state");
            var code = Query(request, "code");
            if (challenge is null || string.IsNullOrEmpty(code) || code.Length > 4096 || Query(request, "error") is not null ||
                !challenge.TryGetPayloadValue<string>("state", out var expectedState) || !FixedEquals(state, expectedState) ||
                !challenge.TryGetPayloadValue<string>("provider", out var provider) || provider != ProviderId ||
                !challenge.TryGetPayloadValue<string>("nonce", out var nonce) ||
                !challenge.TryGetPayloadValue<string>("verifier", out var verifier)) { return null; }
            var issuer = Query(request, "iss");
            if (issuer is not null && issuer != _settings.Authority) { return null; }
            if (!tokens.ConsumeChallenge(challenge)) { return null; }
            var configuration = await GetConfigurationAsync();
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = _settings.RedirectUri,
                ["client_id"] = _settings.ClientId, ["code_verifier"] = verifier
            };
            if (!string.IsNullOrEmpty(_settings.ClientSecret)) { form["client_secret"] = _settings.ClientSecret; }
            using var content = new FormUrlEncodedContent(form);
            using var response = await _client.PostAsync(configuration.TokenEndpoint, content);
            if (!response.IsSuccessStatusCode) { return null; }
            var body = await response.Content.ReadAsStringAsync();
            if (body.Length > 65536) { return null; }
            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("id_token", out var token) || token.ValueKind != JsonValueKind.String) { return null; }
            var result = await ValidateIdTokenAsync(token.GetString(), configuration);
            if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                _configuration.RequestRefresh();
                configuration = await GetConfigurationAsync();
                result = await ValidateIdTokenAsync(token.GetString(), configuration);
            }
            if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt || string.IsNullOrWhiteSpace(jwt.Subject) ||
                !jwt.TryGetPayloadValue<string>("nonce", out var actualNonce) || !FixedEquals(nonce, actualNonce) ||
                jwt.IssuedAt == DateTime.MinValue || jwt.IssuedAt > DateTime.UtcNow.AddSeconds(30)) { return null; }
            jwt.TryGetPayloadValue<string>("azp", out var authorizedParty);
            if ((jwt.Audiences.Count() > 1 && authorizedParty is null) ||
                (authorizedParty is not null && authorizedParty != _settings.ClientId)) { return null; }
            return MapIdentity(jwt);
        }

        /// <summary>
        /// Preserves role labels for application display while granting only explicitly configured local rights.
        /// The issuer participates in the subject identifier so different authorities cannot collide by subject.
        /// </summary>
        /// <param name="token">The verified external ID token whose claims are mapped to the common identity.</param>
        /// <returns>The common identity containing only locally mapped authorization claims.</returns>
        protected virtual IIdentity MapIdentity(JsonWebToken token)
        {
            var roles = ReadRoles(token).Distinct(StringComparer.Ordinal).ToArray();
            token.TryGetPayloadValue<string>("preferred_username", out var name);
            token.TryGetPayloadValue<string>("email", out var email);
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(_settings.Authority + "\0" + token.Subject)).AsSpan(0, 16));
            return new Identity(id, string.IsNullOrWhiteSpace(name) ? token.Subject : name, email, roles,
                roles.Where(_settings.RolePermissions.ContainsKey).SelectMany(x => _settings.RolePermissions[x]),
                roles.Where(_settings.RolePolicies.ContainsKey).SelectMany(x => _settings.RolePolicies[x]));
        }

        /// <summary>
        /// Allows provider-specific role claim layouts without changing token validation or local authorization rules.
        /// </summary>
        /// <param name="token">The verified external ID token containing the provider's role claims.</param>
        /// <returns>The role labels supplied by the verified provider-specific claim mapping.</returns>
        protected virtual IEnumerable<string> ReadRoles(JsonWebToken token)
        {
            return token.TryGetPayloadValue<string[]>("roles", out var roles) ? roles : [];
        }

        /// <summary>
        /// Exposes the audience used to restrict provider-specific client role claims.
        /// </summary>
        protected string ClientId => _settings.ClientId;

        /// <summary>
        /// Validates external identity evidence against pinned authority metadata and asymmetric algorithms.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="configuration">The trusted authority metadata containing signing keys and protocol endpoints.</param>
        /// <returns>The cryptographic and protocol claim validation result.</returns>
        private Task<TokenValidationResult> ValidateIdTokenAsync(string token, OpenIdConnectConfiguration configuration)
        {
            return _handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = _settings.Authority,
                ValidateAudience = true, ValidAudience = _settings.ClientId,
                ValidateIssuerSigningKey = true, IssuerSigningKeys = configuration.SigningKeys,
                RequireSignedTokens = true, RequireExpirationTime = true, ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
                    SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256]
            });
        }

        /// <summary>
        /// Checks discovered endpoints against the configured HTTPS authority before code exchange.
        /// </summary>
        /// <returns>The verified discovery metadata for the configured authority.</returns>
        private async Task<OpenIdConnectConfiguration> GetConfigurationAsync()
        {
            var configuration = await _configuration.GetConfigurationAsync(CancellationToken.None);
            if (configuration.Issuer != _settings.Authority) { throw new InvalidOperationException("OIDC discovery issuer mismatch."); }
            RequireHttps(configuration.AuthorizationEndpoint);
            RequireHttps(configuration.TokenEndpoint);
            RequireHttps(configuration.JwksUri);
            return configuration;
        }

        /// <summary>
        /// Excludes ambiguous, session, and form values from authorization-code correlation.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="name">The exact cookie or claim name to inspect.</param>
        /// <returns>The single decoded query value, or null when absent or ambiguous.</returns>
        internal static string Query(IRequest request, string name)
        {
            if (request is not RequestBase concrete) { return null; }
            var query = QueryHelpers.ParseQuery(concrete.QueryString);
            return query.TryGetValue(name, out var values) && values.Count == 1 ? values[0] : null;
        }

        /// <summary>
        /// Avoids content-dependent comparisons of browser correlation secrets.
        /// </summary>
        /// <param name="left">The correlation value received for comparison.</param>
        /// <param name="right">The expected correlation value.</param>
        /// <returns>True when both correlation values match; otherwise, false.</returns>
        private static bool FixedEquals(string left, string right) => left is not null && right is not null &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

        /// <summary>
        /// Rejects external endpoints that could disclose credentials through insecure transport.
        /// </summary>
        /// <param name="value">The value being validated or placed in the protected response.</param>
        private static void RequireHttps(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ArgumentException("OIDC endpoints must use absolute HTTPS URLs without credentials.");
            }
        }

        /// <summary>
        /// External directories are not enumerated to authenticate an already verified subject.
        /// </summary>
        /// <returns>The directory identities, or an empty collection for an external source.</returns>
        public IEnumerable<IIdentity> GetIdentities() => [];
        /// <summary>
        /// External roles are mapped at login without importing a remote group directory.
        /// </summary>
        /// <returns>The local directory groups, or an empty collection when no directory is exposed.</returns>
        public IEnumerable<IIdentityGroup> GetGroups() => [];
        /// <summary>
        /// Leaves the choice of interactive provider to the application's login UI.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="initiator">The protected page that initiated authentication or denied access.</param>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <returns>The provider response, or null when the application should choose the login presentation.</returns>
        public IResponse CreateAuthenticationPrompt(IRequest request, IPageContext initiator, IIdentity identity) => null;
        /// <summary>
        /// Lets the application retain control of its permission-denied presentation.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="initiator">The protected page that initiated authentication or denied access.</param>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <returns>The provider response, or null when the application should choose the denied-access presentation.</returns>
        public IResponse CreateForbiddenResponse(IRequest request, IPageContext initiator, IIdentity identity) => null;
        /// <summary>
        /// Releases the provider-owned backchannel when its plugin is removed.
        /// </summary>
        public void Dispose()
        {
            if (_ownsClient) { _client.Dispose(); }
            GC.SuppressFinalize(this);
        }
    }
}
