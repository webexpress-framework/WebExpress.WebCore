using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Enforces disjoint trust rules for access, refresh, and personal credentials.
    /// Access verification needs only the shared signing configuration and the signed claims; replay
    /// and revocation markers live in the store that <see cref="IIdentityTokenStoreManager"/> binds to the application.
    /// </summary>
    public partial class IdentityManager
    {
        private readonly JsonWebTokenHandler _tokenHandler = new() { MaximumTokenSizeInBytes = 16384 };
        private readonly object _authorityGate = new();
        private TokenAuthority _authority;

        /// <summary>
        /// Holds the validated trust boundary so weak secrets are rejected before any credential is signed.
        /// </summary>
        /// <param name="Signing">The deployment-wide HMAC signing credentials.</param>
        /// <param name="Issuer">The authority that issues WebExpress tokens.</param>
        /// <param name="Audience">The audience prefix, completed by the application identifier.</param>
        /// <param name="AccessLifetime">The lifetime of a signed authorization snapshot.</param>
        /// <param name="RefreshLifetime">The absolute lifetime of a login grant.</param>
        /// <param name="PersonalLifetime">The maximum lifetime of a personal credential.</param>
        private sealed record TokenAuthority(SigningCredentials Signing, string Issuer, string Audience,
            TimeSpan AccessLifetime, TimeSpan RefreshLifetime, TimeSpan PersonalLifetime);

        /// <summary>
        /// Gets or sets the UTC clock that enforces signed expiration boundaries, replaceable so tests can cross deadlines without sleeping.
        /// </summary>
        internal TimeProvider Clock { get; set; } = TimeProvider.System;

        /// <summary>
        /// Loads the signing authority once and rejects missing or weak deployment secrets instead of falling back to per-process keys.
        /// </summary>
        private TokenAuthority Authority
        {
            get
            {
                lock (_authorityGate)
                {
                    if (_authority is not null) { return _authority; }
                    var settings = _httpServerContext.Configuration.GetSection("WebExpress:Authentication").Get<AuthenticationSettings>();
                    if (settings is null) { return null; }
                    ArgumentException.ThrowIfNullOrWhiteSpace(settings.Issuer);
                    ArgumentException.ThrowIfNullOrWhiteSpace(settings.Audience);
                    var key = Convert.FromBase64String(settings.SigningKey ?? "");
                    if (key.Length < 32) { throw new ArgumentException("Authentication requires at least 256 random signing-key bits."); }
                    if (settings.AccessTokenLifetime < TimeSpan.FromSeconds(1) ||
                        settings.RefreshTokenLifetime <= settings.AccessTokenLifetime ||
                        settings.MaximumPersonalAccessTokenLifetime < TimeSpan.FromSeconds(1))
                    {
                        throw new ArgumentException("Authentication token lifetimes are invalid.");
                    }
                    _authority = new TokenAuthority(new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
                        settings.Issuer, settings.Audience, settings.AccessTokenLifetime, settings.RefreshTokenLifetime,
                        settings.MaximumPersonalAccessTokenLifetime);
                    return _authority;
                }
            }
        }

        /// <summary>
        /// Reports whether the application can both sign credentials and persist their replay and revocation markers.
        /// </summary>
        /// <param name="applicationContext">The application whose authentication endpoints are being served.</param>
        /// <returns>True when a signing authority and a token store are available; otherwise, false.</returns>
        internal bool IsAuthenticationConfigured(IApplicationContext applicationContext)
        {
            return Authority is not null && TokenStore(applicationContext) is not null;
        }

        /// <summary>
        /// Gives every authentication source the same short-lived authorization snapshot.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <returns>The signed access and refresh credentials for the new login grant.</returns>
        public IdentityTokenPair Issue(IIdentity identity, IApplicationContext applicationContext)
        {
            return IssuePair(identity, applicationContext, Guid.NewGuid().ToString("N"), Clock.GetUtcNow() + RequireAuthority().RefreshLifetime);
        }

        /// <summary>
        /// Accepts access credentials only, preventing refresh or personal tokens from entering through cookies.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <returns>The verified access identity, or null when validation fails.</returns>
        public IIdentity ValidateAccessToken(string token, IApplicationContext applicationContext)
        {
            return ReadIdentity(ValidateToken(token, applicationContext, "access"));
        }

        /// <summary>
        /// Restricts bearer credentials to explicitly created, unrevoked personal tokens.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <returns>The verified personal identity, or null when validation or revocation checks fail.</returns>
        public IIdentity ValidatePersonalAccessToken(string token, IApplicationContext applicationContext)
        {
            var jwt = ValidateToken(token, applicationContext, "pat");
            // without a store a revocation cannot be ruled out, so the credential is refused
            var store = TokenStore(applicationContext);
            return jwt is not null && store is not null && !store.IsRevoked("pat:" + jwt.Id) ? ReadIdentity(jwt) : null;
        }

        /// <summary>
        /// Rotates each refresh credential once and revokes the entire grant when it is replayed.
        /// The original grant deadline is retained so renewal cannot make a login immortal.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <returns>The rotated token pair, or null when validation or replay protection rejects renewal.</returns>
        public IdentityTokenPair Refresh(string token, IApplicationContext applicationContext)
        {
            var jwt = ValidateToken(token, applicationContext, "refresh");
            var identity = ReadIdentity(jwt);
            var store = TokenStore(applicationContext);
            if (identity is null || store is null || !jwt.TryGetPayloadValue<string>("grant", out var grant) ||
                string.IsNullOrEmpty(grant) || store.IsRevoked("grant:" + grant)) { return null; }
            var expires = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
            if (!store.TryConsume("refresh:" + jwt.Id, expires))
            {
                store.Revoke("grant:" + grant, expires);
                return null;
            }
            return IssuePair(identity, applicationContext, grant, expires);
        }

        /// <summary>
        /// Persists revocation so the personal credential stops working on every instance sharing the application's store.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <returns>True when a valid personal credential was revoked; otherwise, false.</returns>
        public bool RevokePersonalAccessToken(string token, IApplicationContext applicationContext)
        {
            RequireAuthority();
            var jwt = ValidateToken(token, applicationContext, "pat");
            if (jwt is null) { return false; }
            RequireTokenStore(applicationContext).Revoke("pat:" + jwt.Id, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
            return true;
        }

        /// <summary>
        /// Ends renewal using the grant reference in an access cookie, even when the refresh cookie's
        /// narrow path keeps it out of the logout request. Existing access tokens expire naturally.
        /// </summary>
        /// <param name="accessToken">The signed access credential identifying the login grant to revoke.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        public void RevokeGrant(string accessToken, IApplicationContext applicationContext)
        {
            var jwt = ValidateToken(accessToken, applicationContext, "access", validateLifetime: false);
            if (jwt is not null && jwt.TryGetPayloadValue<string>("grant", out var grant) &&
                jwt.TryGetPayloadValue<long>("grant_exp", out var expiration))
            {
                RequireTokenStore(applicationContext).Revoke("grant:" + grant, DateTimeOffset.FromUnixTimeSeconds(expiration));
            }
        }

        /// <summary>
        /// Signs correlation data with a purpose that cannot authorize application requests.
        /// </summary>
        /// <param name="claims">The trusted claims bound into the signed credential.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <returns>The signed browser correlation credential.</returns>
        internal string ProtectChallenge(Dictionary<string, object> claims, IApplicationContext applicationContext)
        {
            return SignToken(claims, applicationContext, "challenge", Clock.GetUtcNow() + TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Accepts only unexpired browser correlation credentials for the selected application.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <returns>The verified correlation token, or null when validation fails.</returns>
        internal JsonWebToken ValidateChallenge(string token, IApplicationContext applicationContext)
        {
            return ValidateToken(token, applicationContext, "challenge");
        }

        /// <summary>
        /// Prevents different server instances from exchanging a code with the same challenge.
        /// </summary>
        /// <param name="challenge">The verified browser correlation token that must be consumed exactly once.</param>
        /// <param name="applicationContext">The application whose store records the consumption.</param>
        /// <returns>True for the first successful consumption; otherwise, false.</returns>
        internal bool ConsumeChallenge(JsonWebToken challenge, IApplicationContext applicationContext)
        {
            return TokenStore(applicationContext)?.TryConsume("challenge:" + challenge.Id,
                new DateTimeOffset(challenge.ValidTo, TimeSpan.Zero)) ?? false;
        }

        /// <summary>
        /// Allows logout at the refresh cookie path to end the original login grant.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        internal void RevokeRefreshGrant(string token, IApplicationContext applicationContext)
        {
            var jwt = ValidateToken(token, applicationContext, "refresh");
            if (jwt is not null && jwt.TryGetPayloadValue<string>("grant", out var grant))
            {
                RequireTokenStore(applicationContext).Revoke("grant:" + grant, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
            }
        }

        /// <summary>
        /// Creates an explicitly bounded credential and intersects its permissions with its owner's grants.
        /// Policies and roles are omitted so they cannot bypass a PAT's permission restriction.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <param name="lifetime">The explicit validity period requested for the personal credential.</param>
        /// <param name="permissions">The requested permission identifiers, limited to the owner's grants.</param>
        /// <returns>The signed personal credential with the requested authorized permissions.</returns>
        private string SignPersonalAccessToken(IIdentity identity, IApplicationContext applicationContext, TimeSpan lifetime,
            IEnumerable<string> permissions)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(permissions);
            if (lifetime < TimeSpan.FromSeconds(1) || lifetime > RequireAuthority().PersonalLifetime) { throw new ArgumentOutOfRangeException(nameof(lifetime)); }
            var requested = permissions.Distinct(StringComparer.Ordinal).ToArray();
            if (requested.Except(identity.Permissions, StringComparer.Ordinal).Any())
            {
                throw new ArgumentException("A personal token cannot exceed its owner's permissions.", nameof(permissions));
            }
            var restricted = new Identity(identity.Id, identity.Name, identity.Email, permissions: requested);
            return SignIdentity(restricted, applicationContext, "pat", Clock.GetUtcNow() + lifetime);
        }

        /// <summary>
        /// Keeps both credentials inside the absolute grant deadline and browser cookie size limits.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <param name="grant">The identifier of the original login grant.</param>
        /// <param name="deadline">The absolute deadline beyond which the login grant cannot be renewed.</param>
        /// <returns>The signed token pair constrained by the absolute grant deadline.</returns>
        private IdentityTokenPair IssuePair(IIdentity identity, IApplicationContext applicationContext, string grant, DateTimeOffset deadline)
        {
            ArgumentNullException.ThrowIfNull(identity);
            var accessDeadline = Clock.GetUtcNow() + RequireAuthority().AccessLifetime;
            if (accessDeadline > deadline) { accessDeadline = deadline; }
            var pair = new IdentityTokenPair
            {
                AccessToken = SignIdentity(identity, applicationContext, "access", accessDeadline, grant, deadline),
                RefreshToken = SignIdentity(identity, applicationContext, "refresh", deadline, grant, deadline),
                AccessTokenExpiresAt = accessDeadline,
                RefreshTokenExpiresAt = deadline
            };
            if (pair.AccessToken.Length > 3800 || pair.RefreshToken.Length > 3800)
            {
                throw new InvalidOperationException("Identity claims exceed the authentication cookie limit.");
            }
            return pair;
        }

        /// <summary>
        /// Copies only credential-free identity claims into a purpose-specific payload.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <param name="purpose">The token purpose separating access, renewal, and browser correlation.</param>
        /// <param name="expires">The signed expiration deadline of the credential.</param>
        /// <param name="grant">The identifier of the original login grant.</param>
        /// <param name="grantExpires">The expiration deadline shared by credentials from one login grant.</param>
        /// <returns>The signed credential containing the trusted identity snapshot.</returns>
        private string SignIdentity(IIdentity identity, IApplicationContext applicationContext, string purpose, DateTimeOffset expires,
            string grant = null, DateTimeOffset? grantExpires = null)
        {
            var claims = new Dictionary<string, object>
            {
                ["sub"] = identity.Id.ToString(), ["name"] = identity.Name,
                ["roles"] = identity.Roles.ToArray(), ["permissions"] = identity.Permissions.ToArray(),
                ["policies"] = identity.PolicyNames.ToArray()
            };
            if (identity.Email is not null) { claims["email"] = identity.Email; }
            if (grant is not null) { claims["grant"] = grant; claims["grant_exp"] = grantExpires.Value.ToUnixTimeSeconds(); }
            return SignToken(claims, applicationContext, purpose, expires);
        }

        /// <summary>
        /// Binds claims to their signing authority, application, purpose, and expiration.
        /// </summary>
        /// <param name="claims">The trusted claims bound into the signed credential.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <param name="purpose">The token purpose separating access, renewal, and browser correlation.</param>
        /// <param name="expires">The signed expiration deadline of the credential.</param>
        /// <returns>The signed JWT bound to the deployment trust boundary.</returns>
        private string SignToken(Dictionary<string, object> claims, IApplicationContext applicationContext, string purpose, DateTimeOffset expires)
        {
            var authority = RequireAuthority();
            ArgumentNullException.ThrowIfNull(applicationContext);
            ArgumentException.ThrowIfNullOrWhiteSpace(applicationContext.ApplicationId);
            claims["jti"] = Guid.NewGuid().ToString("N");
            claims["token_use"] = purpose;
            return _tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = authority.Issuer, Audience = authority.Audience + "/" + applicationContext.ApplicationId,
                IssuedAt = Clock.GetUtcNow().UtcDateTime, NotBefore = Clock.GetUtcNow().UtcDateTime,
                Expires = expires.UtcDateTime, Claims = claims, SigningCredentials = authority.Signing,
                TokenType = "wx-" + purpose + "+jwt"
            });
        }

        /// <summary>
        /// Enforces the complete token trust boundary before any claims are consumed.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application that scopes token audiences and authentication.</param>
        /// <param name="purpose">The token purpose separating access, renewal, and browser correlation.</param>
        /// <param name="validateLifetime">A value indicating whether this operation must enforce expiration.</param>
        /// <returns>The verified JWT, or null when any required trust check fails.</returns>
        private JsonWebToken ValidateToken(string token, IApplicationContext applicationContext, string purpose, bool validateLifetime = true)
        {
            var authority = Authority;
            if (authority is null || string.IsNullOrEmpty(token) || token.Length > 16384 ||
                string.IsNullOrEmpty(applicationContext?.ApplicationId)) { return null; }
            var result = _tokenHandler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = authority.Issuer,
                ValidateAudience = true, ValidAudience = authority.Audience + "/" + applicationContext.ApplicationId,
                ValidateIssuerSigningKey = true, IssuerSigningKey = authority.Signing.Key,
                RequireSignedTokens = true, RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidTypes = ["wx-" + purpose + "+jwt"],
                ValidateLifetime = validateLifetime, ClockSkew = TimeSpan.Zero,
                LifetimeValidator = validateLifetime ? (notBefore, expires, _, _) =>
                    notBefore.HasValue && expires.HasValue && notBefore <= Clock.GetUtcNow().UtcDateTime &&
                    expires > Clock.GetUtcNow().UtcDateTime && notBefore < expires : null
            }).GetAwaiter().GetResult();
            if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt || string.IsNullOrEmpty(jwt.Id) ||
                !jwt.TryGetPayloadValue<string>("token_use", out var use) || use != purpose) { return null; }
            return jwt;
        }

        /// <summary>
        /// Prevents authentication from silently falling back to ephemeral signing configuration.
        /// </summary>
        /// <returns>The signing authority configured for this deployment.</returns>
        private TokenAuthority RequireAuthority()
        {
            return Authority ?? throw new InvalidOperationException("Configure WebExpress:Authentication before signing in.");
        }

        /// <summary>
        /// Resolves the store per call, so a store replaced or removed together with its plugin is never used afterwards.
        /// </summary>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        /// <returns>The store bound to the application, or null when none is available.</returns>
        private IIdentityTokenStore TokenStore(IApplicationContext applicationContext)
        {
            return _componentHub?.IdentityTokenStoreManager?.GetStore(applicationContext);
        }

        /// <summary>
        /// Fails a revocation outright rather than reporting success for a marker that was never persisted.
        /// </summary>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        /// <returns>The store bound to the application.</returns>
        private IIdentityTokenStore RequireTokenStore(IApplicationContext applicationContext)
        {
            return TokenStore(applicationContext) ??
                throw new InvalidOperationException("Configure WebExpress:Authentication:TokenStorePath or register an identity token store.");
        }

        /// <summary>
        /// Rejects incomplete subjects before constructing a credential-free identity.
        /// </summary>
        /// <param name="jwt">The verified JWT from which credential-free claims are read.</param>
        /// <returns>The immutable identity snapshot, or null when required claims are absent.</returns>
        private static IIdentity ReadIdentity(JsonWebToken jwt)
        {
            if (jwt is null || !Guid.TryParse(jwt.Subject, out var subject) || subject == Guid.Empty ||
                !jwt.TryGetPayloadValue<string>("name", out var name) || string.IsNullOrWhiteSpace(name)) { return null; }
            try
            {
                jwt.TryGetPayloadValue<string>("email", out var email);
                return new Identity(subject, name, email, ReadArray(jwt, "roles"), ReadArray(jwt, "permissions"), ReadArray(jwt, "policies"));
            }
            catch (JsonException) { return null; }
        }

        /// <summary>
        /// Reads authorization labels without constructing executable types from serialized claims.
        /// </summary>
        /// <param name="jwt">The verified JWT from which credential-free claims are read.</param>
        /// <param name="name">The exact claim name to inspect.</param>
        /// <returns>The claim values, or an empty array when the claim is absent.</returns>
        private static string[] ReadArray(JsonWebToken jwt, string name)
        {
            return jwt.TryGetPayloadValue<string[]>(name, out var values) ? values : [];
        }
    }
}
