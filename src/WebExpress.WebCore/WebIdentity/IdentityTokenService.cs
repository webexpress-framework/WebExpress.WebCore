using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Enforces disjoint trust rules for access, refresh, and personal credentials.
    /// Access verification needs only the shared signing configuration and the signed claims.
    /// </summary>
    public sealed class IdentityTokenService
    {
        private readonly JsonWebTokenHandler _handler = new() { MaximumTokenSizeInBytes = 16384 };
        private readonly SigningCredentials _signing;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly TimeSpan _accessLifetime;
        private readonly TimeSpan _refreshLifetime;
        private readonly TimeSpan _patLifetime;
        private readonly IIdentityTokenStore _store;
        private readonly TimeProvider _time;

        /// <summary>
        /// Rejects missing or weak deployment secrets instead of falling back to per-process keys.
        /// </summary>
        /// <param name="settings">The configured trust boundary and credential lifetime constraints.</param>
        /// <param name="store">The durable store shared by instances that consume or revoke credentials.</param>
        /// <param name="timeProvider">The UTC clock used to enforce signed expiration boundaries.</param>
        public IdentityTokenService(AuthenticationSettings settings, IIdentityTokenStore store, TimeProvider timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(store);
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
            _signing = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
            _issuer = settings.Issuer;
            _audience = settings.Audience;
            _accessLifetime = settings.AccessTokenLifetime;
            _refreshLifetime = settings.RefreshTokenLifetime;
            _patLifetime = settings.MaximumPersonalAccessTokenLifetime;
            _store = store;
            _time = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Gives every authentication source the same short-lived authorization snapshot.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>The signed access and refresh credentials for the new login grant.</returns>
        public IdentityTokenPair Issue(IIdentity identity, string applicationId)
        {
            return IssuePair(identity, applicationId, Guid.NewGuid().ToString("N"), _time.GetUtcNow() + _refreshLifetime);
        }

        /// <summary>
        /// Accepts access credentials only, preventing refresh or personal tokens from entering through cookies.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>The verified access identity, or null when validation fails.</returns>
        public IIdentity ValidateAccessToken(string token, string applicationId)
        {
            return ReadIdentity(Validate(token, applicationId, "access"));
        }

        /// <summary>
        /// Restricts bearer credentials to explicitly created, unrevoked personal tokens.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>The verified personal identity, or null when validation or revocation checks fail.</returns>
        public IIdentity ValidatePersonalAccessToken(string token, string applicationId)
        {
            var jwt = Validate(token, applicationId, "pat");
            return jwt is not null && !_store.IsRevoked("pat:" + jwt.Id) ? ReadIdentity(jwt) : null;
        }

        /// <summary>
        /// Rotates each refresh credential once and revokes the entire grant when it is replayed.
        /// The original grant deadline is retained so renewal cannot make a login immortal.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>The rotated token pair, or null when validation or replay protection rejects renewal.</returns>
        public IdentityTokenPair Refresh(string token, string applicationId)
        {
            var jwt = Validate(token, applicationId, "refresh");
            var identity = ReadIdentity(jwt);
            if (identity is null || !jwt.TryGetPayloadValue<string>("grant", out var grant) ||
                string.IsNullOrEmpty(grant) || _store.IsRevoked("grant:" + grant)) { return null; }
            var expires = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
            if (!_store.TryConsume("refresh:" + jwt.Id, expires))
            {
                _store.Revoke("grant:" + grant, expires);
                return null;
            }
            return IssuePair(identity, applicationId, grant, expires);
        }

        /// <summary>
        /// Creates an explicitly bounded credential and intersects its permissions with its owner's grants.
        /// Policies and roles are omitted so they cannot bypass a PAT's permission restriction.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <param name="lifetime">The explicit validity period requested for the personal credential.</param>
        /// <param name="permissions">The requested permission identifiers, limited to the owner's grants.</param>
        /// <returns>The signed personal credential with the requested authorized permissions.</returns>
        public string CreatePersonalAccessToken(IIdentity identity, string applicationId, TimeSpan lifetime,
            IEnumerable<string> permissions)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(permissions);
            if (lifetime < TimeSpan.FromSeconds(1) || lifetime > _patLifetime) { throw new ArgumentOutOfRangeException(nameof(lifetime)); }
            var requested = permissions.Distinct(StringComparer.Ordinal).ToArray();
            if (requested.Except(identity.Permissions, StringComparer.Ordinal).Any())
            {
                throw new ArgumentException("A personal token cannot exceed its owner's permissions.", nameof(permissions));
            }
            var restricted = new Identity(identity.Id, identity.Name, identity.Email, permissions: requested);
            return Create(restricted, applicationId, "pat", _time.GetUtcNow() + lifetime);
        }

        /// <summary>
        /// Prevents a personal credential from authenticating on any instance sharing the token store.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>True when a valid personal credential was revoked; otherwise, false.</returns>
        public bool RevokePersonalAccessToken(string token, string applicationId)
        {
            var jwt = Validate(token, applicationId, "pat");
            if (jwt is null) { return false; }
            _store.Revoke("pat:" + jwt.Id, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
            return true;
        }

        /// <summary>
        /// Ends renewal using the grant reference in an access cookie, even when the refresh cookie's
        /// narrow path keeps it out of the logout request. Existing access tokens expire naturally.
        /// </summary>
        /// <param name="accessToken">The signed access credential identifying the login grant to revoke.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        public void RevokeGrant(string accessToken, string applicationId)
        {
            var jwt = Validate(accessToken, applicationId, "access", validateLifetime: false);
            if (jwt is not null && jwt.TryGetPayloadValue<string>("grant", out var grant) &&
                jwt.TryGetPayloadValue<long>("grant_exp", out var expiration))
            {
                _store.Revoke("grant:" + grant, DateTimeOffset.FromUnixTimeSeconds(expiration));
            }
        }

        /// <summary>
        /// Signs correlation data with a purpose that cannot authorize application requests.
        /// </summary>
        /// <param name="claims">The trusted claims bound into the signed credential.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>The signed browser correlation credential.</returns>
        internal string ProtectChallenge(Dictionary<string, object> claims, string applicationId)
        {
            return CreateToken(claims, applicationId, "challenge", _time.GetUtcNow() + TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Accepts only unexpired browser correlation credentials for the selected application.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <returns>The verified correlation token, or null when validation fails.</returns>
        internal JsonWebToken ValidateChallenge(string token, string applicationId) => Validate(token, applicationId, "challenge");

        /// <summary>
        /// Prevents different server instances from exchanging a code with the same challenge.
        /// </summary>
        /// <param name="challenge">The verified browser correlation token that must be consumed exactly once.</param>
        /// <returns>True for the first successful consumption; otherwise, false.</returns>
        internal bool ConsumeChallenge(JsonWebToken challenge) => _store.TryConsume("challenge:" + challenge.Id,
            new DateTimeOffset(challenge.ValidTo, TimeSpan.Zero));

        /// <summary>
        /// Allows logout at the refresh cookie path to end the original login grant.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        internal void RevokeRefreshGrant(string token, string applicationId)
        {
            var jwt = Validate(token, applicationId, "refresh");
            if (jwt is not null && jwt.TryGetPayloadValue<string>("grant", out var grant))
            {
                _store.Revoke("grant:" + grant, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
            }
        }

        /// <summary>
        /// Keeps both credentials inside the absolute grant deadline and browser cookie size limits.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <param name="grant">The identifier of the original login grant.</param>
        /// <param name="deadline">The absolute deadline beyond which the login grant cannot be renewed.</param>
        /// <returns>The signed token pair constrained by the absolute grant deadline.</returns>
        private IdentityTokenPair IssuePair(IIdentity identity, string applicationId, string grant, DateTimeOffset deadline)
        {
            ArgumentNullException.ThrowIfNull(identity);
            var accessDeadline = _time.GetUtcNow() + _accessLifetime;
            if (accessDeadline > deadline) { accessDeadline = deadline; }
            var pair = new IdentityTokenPair
            {
                AccessToken = Create(identity, applicationId, "access", accessDeadline, grant, deadline),
                RefreshToken = Create(identity, applicationId, "refresh", deadline, grant, deadline),
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
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <param name="purpose">The token purpose separating access, renewal, and browser correlation.</param>
        /// <param name="expires">The signed expiration deadline of the credential.</param>
        /// <param name="grant">The identifier of the original login grant.</param>
        /// <param name="grantExpires">The expiration deadline shared by credentials from one login grant.</param>
        /// <returns>The signed credential containing the trusted identity snapshot.</returns>
        private string Create(IIdentity identity, string applicationId, string purpose, DateTimeOffset expires,
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
            return CreateToken(claims, applicationId, purpose, expires);
        }

        /// <summary>
        /// Binds claims to their signing authority, application, purpose, and expiration.
        /// </summary>
        /// <param name="claims">The trusted claims bound into the signed credential.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <param name="purpose">The token purpose separating access, renewal, and browser correlation.</param>
        /// <param name="expires">The signed expiration deadline of the credential.</param>
        /// <returns>The signed JWT bound to the deployment trust boundary.</returns>
        private string CreateToken(Dictionary<string, object> claims, string applicationId, string purpose, DateTimeOffset expires)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
            claims["jti"] = Guid.NewGuid().ToString("N");
            claims["token_use"] = purpose;
            return _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _issuer, Audience = _audience + "/" + applicationId,
                IssuedAt = _time.GetUtcNow().UtcDateTime, NotBefore = _time.GetUtcNow().UtcDateTime,
                Expires = expires.UtcDateTime, Claims = claims, SigningCredentials = _signing,
                TokenType = "wx-" + purpose + "+jwt"
            });
        }

        /// <summary>
        /// Enforces the complete token trust boundary before any claims are consumed.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationId">The application identifier that scopes token audiences and authentication.</param>
        /// <param name="purpose">The token purpose separating access, renewal, and browser correlation.</param>
        /// <param name="validateLifetime">A value indicating whether this operation must enforce expiration.</param>
        /// <returns>The verified JWT, or null when any required trust check fails.</returns>
        private JsonWebToken Validate(string token, string applicationId, string purpose, bool validateLifetime = true)
        {
            if (string.IsNullOrEmpty(token) || token.Length > 16384 || string.IsNullOrEmpty(applicationId)) { return null; }
            var result = _handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = _issuer,
                ValidateAudience = true, ValidAudience = _audience + "/" + applicationId,
                ValidateIssuerSigningKey = true, IssuerSigningKey = _signing.Key,
                RequireSignedTokens = true, RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidTypes = ["wx-" + purpose + "+jwt"],
                ValidateLifetime = validateLifetime, ClockSkew = TimeSpan.Zero,
                LifetimeValidator = validateLifetime ? (notBefore, expires, _, _) =>
                    notBefore.HasValue && expires.HasValue && notBefore <= _time.GetUtcNow().UtcDateTime &&
                    expires > _time.GetUtcNow().UtcDateTime && notBefore < expires : null
            }).GetAwaiter().GetResult();
            if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt || string.IsNullOrEmpty(jwt.Id) ||
                !jwt.TryGetPayloadValue<string>("token_use", out var use) || use != purpose) { return null; }
            return jwt;
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
        /// <param name="name">The exact cookie or claim name to inspect.</param>
        /// <returns>The claim values, or an empty array when the claim is absent.</returns>
        private static string[] ReadArray(JsonWebToken jwt, string name)
        {
            return jwt.TryGetPayloadValue<string[]>(name, out var values) ? values : [];
        }
    }
}
