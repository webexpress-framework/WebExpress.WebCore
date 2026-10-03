using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Exercises credential trust boundaries independently of any server-side identity session.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestIdentityAuthentication
    {
        /// <summary>
        /// Proves that login cookies authenticate on another instance and contain no password verifier.
        /// </summary>
        [Fact]
        public void LoginSurvivesInstanceChangeWithoutSession()
        {
            using var fixture = new AuthenticationFixture();
            var source = MockIdentityFactory.GetIdentity("Alice");
            var request = fixture.Request();
            var pair = fixture.Manager.Login(source, request);
            Assert.Null(request.ExistingSession);
            Assert.Null(fixture.Manager.Login(null, request));
            var other = fixture.Instance();
            var identity = other.ValidateAccessToken(pair.AccessToken, fixture.Application);
            Assert.Equal(source.Id, identity.Id);
            Assert.Equal(source.Roles, identity.Roles);
            Assert.Null(identity.PasswordHash);
            var payload = new JsonWebToken(pair.AccessToken).EncodedPayload;
            Assert.DoesNotContain(source.PasswordHash, Base64UrlEncoder.Decode(payload));
            Assert.Contains(typeof(TestIdentityPermissionC).FullName, identity.Permissions);
            Assert.True(fixture.Manager.CheckAccess(fixture.Application, identity, typeof(TestIdentityPermissionC)));
            Assert.True(fixture.Manager.CheckAccess(identity, new TestIdentityPolicyA()));

            var response = new ResponseOK();
            fixture.Manager.ApplyAuthenticationCookies(request, response);
            var access = response.Header.Cookies[IdentityManager.AccessCookieName];
            var refresh = response.Header.Cookies[IdentityManager.RefreshCookieName];
            Assert.True(access.Secure && access.HttpOnly && refresh.Secure && refresh.HttpOnly);
            Assert.Equal("/", access.Path);
            Assert.Equal("/api/auth/refresh", refresh.Path);
            Assert.Equal("no-store", response.Header.CacheControl);
        }

        /// <summary>
        /// A session identifier, a refresh cookie, or a rejected explicit bearer credential cannot impersonate a user.
        /// </summary>
        [Fact]
        public void RequestAuthenticationRejectsWrongCredentialChannels()
        {
            using var fixture = new AuthenticationFixture();
            var request = fixture.Request();
            var pair = fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), request);
            var cookie = $"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n";
            Assert.NotNull(fixture.Manager.GetCurrentIdentity(fixture.Request(cookie)));
            Assert.Null(fixture.Manager.GetCurrentIdentity(fixture.Request($"Cookie: session={Guid.NewGuid()}\r\n")));
            Assert.Null(fixture.Manager.GetCurrentIdentity(fixture.Request($"Cookie: {IdentityManager.AccessCookieName}={pair.RefreshToken}\r\n")));
            Assert.Null(fixture.Manager.GetCurrentIdentity(fixture.Request(cookie + "Authorization: Bearer invalid\r\n")));
            Assert.Null(fixture.Manager.GetCurrentIdentity(fixture.Request($"Authorization: Bearer {pair.AccessToken}\r\n")));
            var anotherApplication = fixture.Request(cookie);
            anotherApplication.ApplicationContext = fixture.Hub.ApplicationManager.GetApplications(typeof(TestApplicationB)).First();
            Assert.Null(fixture.Manager.GetCurrentIdentity(anotherApplication));
        }

        /// <summary>
        /// A signing key the configuration gets wrong leaves every credential unverifiable. That has
        /// to read as "not signed in" - a throw would fail each request that carries a cookie and
        /// hand the exception to the error page an anonymous caller sees.
        /// </summary>
        /// <param name="signingKey">The rejected signing key.</param>
        [Theory]
        [InlineData("not base64!")]
        [InlineData("c2hvcnQ=")]
        [InlineData("")]
        public void RejectedSigningKeyReadsAsUnauthenticated(string signingKey)
        {
            using var fixture = new AuthenticationFixture();
            var pair = fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), fixture.Request());
            var cookie = $"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n";
            fixture.Settings.SigningKey = signingKey;
            var misconfigured = fixture.Instance();

            var exception = Record.Exception(() => Assert.Null(misconfigured.GetCurrentIdentity(fixture.Request(cookie))));

            Assert.Null(exception);
            Assert.False(misconfigured.IsAuthenticationConfigured(fixture.Application));
        }

        /// <summary>
        /// A page checks policies once per protected fragment; the identity behind a request is
        /// verified once and reused for the rest of it, while the next request verifies anew.
        /// </summary>
        [Fact]
        public void IdentityIsVerifiedOncePerRequest()
        {
            using var fixture = new AuthenticationFixture();
            var pair = fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), fixture.Request());
            var cookie = $"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n";
            var request = fixture.Request(cookie);
            var identity = fixture.Manager.GetCurrentIdentity(request);

            // past expiry a fresh verification fails, so only the reused result still names Alice
            fixture.Clock.Now = pair.AccessTokenExpiresAt.AddSeconds(1);

            Assert.NotNull(identity);
            Assert.Same(identity, fixture.Manager.GetCurrentIdentity(request));
            Assert.Null(fixture.Manager.GetCurrentIdentity(fixture.Request(cookie)));

            // reusing the result changes nothing about the credentials, so no cookie is queued
            var response = new ResponseOK();
            fixture.Manager.ApplyAuthenticationCookies(request, response);
            Assert.Empty(response.Header.Cookies.Cast<System.Net.Cookie>());
        }

        /// <summary>
        /// Signature, audience, issuer, expiration, and token purpose remain mandatory even when claims look plausible.
        /// </summary>
        [Fact]
        public void TokenValidationEnforcesEveryTrustBoundary()
        {
            using var fixture = new AuthenticationFixture();
            var identity = new Identity(Guid.NewGuid(), "alice", permissions: ["read"]);
            var app = fixture.Application;
            var pair = fixture.Manager.Issue(identity, app);
            var parts = pair.AccessToken.Split('.');
            parts[1] = Base64UrlEncoder.Encode(Base64UrlEncoder.Decode(parts[1]).Replace("alice", "admin"));
            Assert.Null(fixture.Manager.ValidateAccessToken(string.Join('.', parts), app));
            Assert.Null(fixture.Manager.ValidateAccessToken(pair.AccessToken, fixture.Hub.ApplicationManager.GetApplications(typeof(TestApplicationB)).First()));
            Assert.Null(fixture.Manager.ValidateAccessToken(pair.RefreshToken, app));
            Assert.Null(fixture.Manager.Refresh(pair.AccessToken, app));
            Assert.Null(fixture.Manager.ValidateAccessToken("not.a.jwt", app));
            fixture.Settings.Issuer = "https://another-authority.test";
            var wrongIssuer = fixture.Instance();
            Assert.Null(wrongIssuer.ValidateAccessToken(pair.AccessToken, app));
            fixture.Settings.Issuer = "https://webexpress.test";
            fixture.Settings.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var wrongKey = fixture.Instance();
            Assert.Null(wrongKey.ValidateAccessToken(pair.AccessToken, app));
            fixture.Clock.Now = pair.AccessTokenExpiresAt.AddSeconds(1);
            Assert.Null(fixture.Manager.ValidateAccessToken(pair.AccessToken, app));
            Assert.NotNull(fixture.Manager.Refresh(pair.RefreshToken, app));
        }

        /// <summary>
        /// The signature alone decides acceptance: identical header and claims are honored under the
        /// deployment key and refused under any other key, so knowing the claim layout never suffices.
        /// </summary>
        [Fact]
        public void TokenSignedWithForeignKeyIsRejected()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application;
            var token = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), app).AccessToken;
            var parts = token.Split('.');
            var key = Convert.FromBase64String(fixture.Settings.SigningKey);

            // reproducing the issued token proves the forgery differs from it in nothing but the key
            var resigned = Sign(parts[0], parts[1], key, HMACSHA256.HashData);
            Assert.Equal(token, resigned);
            Assert.NotNull(fixture.Manager.ValidateAccessToken(resigned, app));

            var forged = Sign(parts[0], parts[1], RandomNumberGenerator.GetBytes(32), HMACSHA256.HashData);
            Assert.Null(fixture.Manager.ValidateAccessToken(forged, app));
        }

        /// <summary>
        /// A damaged, truncated, or absent signature is refused instead of being treated as optional.
        /// </summary>
        [Fact]
        public void TamperedOrMissingSignatureIsRejected()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application;
            var token = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), app).AccessToken;
            var parts = token.Split('.');
            Assert.NotNull(fixture.Manager.ValidateAccessToken(token, app));

            // flipped on the decoded bytes, because altering the last base64url character may only touch
            // padding bits and decode to the very same signature
            var signature = Base64UrlEncoder.DecodeBytes(parts[2]);
            signature[^1] ^= 0x01;
            Assert.Null(fixture.Manager.ValidateAccessToken($"{parts[0]}.{parts[1]}.{Base64UrlEncoder.Encode(signature)}", app));
            Assert.Null(fixture.Manager.ValidateAccessToken($"{parts[0]}.{parts[1]}.{Base64UrlEncoder.Encode(signature[..^1])}", app));
            Assert.Null(fixture.Manager.ValidateAccessToken($"{parts[0]}.{parts[1]}.", app));
            Assert.Null(fixture.Manager.ValidateAccessToken($"{parts[0]}.{parts[1]}", app));
        }

        /// <summary>
        /// The header cannot choose how the signature is checked: an unsigned token and a token correctly
        /// signed with the deployment key under another algorithm are both refused.
        /// </summary>
        [Fact]
        public void SignatureAlgorithmCannotBeChosenByToken()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application;
            var parts = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), app).AccessToken.Split('.');
            var key = Convert.FromBase64String(fixture.Settings.SigningKey);
            var header = Base64UrlEncoder.Decode(parts[0]);
            Assert.Contains("\"HS256\"", header);

            var unsigned = Base64UrlEncoder.Encode(header.Replace("\"HS256\"", "\"none\""));
            Assert.Null(fixture.Manager.ValidateAccessToken($"{unsigned}.{parts[1]}.", app));

            var hs512 = Base64UrlEncoder.Encode(header.Replace("\"HS256\"", "\"HS512\""));
            Assert.Null(fixture.Manager.ValidateAccessToken(Sign(hs512, parts[1], key, HMACSHA512.HashData), app));
        }

        /// <summary>
        /// Builds a compact JWS by hand so a test controls header, payload, key, and algorithm independently
        /// of the token handler under test.
        /// </summary>
        /// <param name="header">The base64url-encoded protected header.</param>
        /// <param name="payload">The base64url-encoded claim set.</param>
        /// <param name="key">The HMAC key that produces the signature.</param>
        /// <param name="hmac">The HMAC function matching the algorithm named in the header.</param>
        /// <returns>The serialized token in compact form.</returns>
        private static string Sign(string header, string payload, byte[] key, Func<byte[], byte[], byte[]> hmac)
        {
            var signature = hmac(key, System.Text.Encoding.ASCII.GetBytes(header + "." + payload));
            return header + "." + payload + "." + Base64UrlEncoder.Encode(signature);
        }

        /// <summary>
        /// Refresh replay revokes its successor across instances and renewal never moves the absolute grant deadline.
        /// </summary>
        [Fact]
        public void RefreshRotatesOnceAndRetainsAbsoluteExpiry()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application;
            var pair = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), app);
            fixture.Clock.Now += TimeSpan.FromMinutes(10);
            var next = fixture.Manager.Refresh(pair.RefreshToken, app);
            Assert.NotEqual(pair.RefreshToken, next.RefreshToken);
            Assert.Equal(pair.RefreshTokenExpiresAt.ToUnixTimeSeconds(), next.RefreshTokenExpiresAt.ToUnixTimeSeconds());
            var other = fixture.Instance();
            Assert.Null(other.Refresh(pair.RefreshToken, app));
            Assert.Null(fixture.Manager.Refresh(next.RefreshToken, app));
            var fresh = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "bob"), app);
            fixture.Clock.Now = fresh.RefreshTokenExpiresAt.AddSeconds(1);
            Assert.Null(fixture.Manager.Refresh(fresh.RefreshToken, app));
        }

        /// <summary>
        /// Exclusive marker creation permits only one winner when different nodes redeem the same refresh token.
        /// </summary>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Fact]
        public async Task ConcurrentRefreshHasOnlyOneWinner()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application;
            var pair = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), app);
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                fixture.Instance()
                    .Refresh(pair.RefreshToken, app))));
            Assert.Single(results, x => x is not null);
        }

        /// <summary>
        /// PAT permissions cannot grow through role or policy claims, renewal, or issuance of broader credentials.
        /// </summary>
        [Fact]
        public void PersonalTokensAreScopedExpiringAndRevocable()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application;
            var owner = new Identity(Guid.NewGuid(), "alice", roles: ["admin"], permissions: ["read", "write"], policyNames: ["admin-policy"]);
            var token = fixture.Manager.CreatePersonalAccessToken(owner, app, TimeSpan.FromHours(1), ["read"]);
            var restricted = fixture.Manager.ValidatePersonalAccessToken(token, app);
            Assert.Equal(["read"], restricted.Permissions);
            Assert.Empty(restricted.Roles);
            Assert.Empty(restricted.PolicyNames);
            Assert.Null(fixture.Manager.ValidateAccessToken(token, app));
            Assert.Null(fixture.Manager.Refresh(token, app));
            Assert.Throws<ArgumentException>(() => fixture.Manager.CreatePersonalAccessToken(owner, app, TimeSpan.FromHours(1), ["delete"]));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Manager.CreatePersonalAccessToken(owner, app, TimeSpan.Zero, ["read"]));
            Assert.True(fixture.Manager.RevokePersonalAccessToken(token, app));
            var other = fixture.Instance();
            Assert.Null(other.ValidatePersonalAccessToken(token, app));
            var expiring = fixture.Manager.CreatePersonalAccessToken(owner, app, TimeSpan.FromSeconds(1), ["read"]);
            fixture.Clock.Now += TimeSpan.FromSeconds(2);
            Assert.Null(fixture.Manager.ValidatePersonalAccessToken(expiring, app));
        }

        /// <summary>
        /// Logout removes browser credentials and prevents renewal without promising immediate access-token revocation.
        /// </summary>
        [Fact]
        public void LogoutRevokesRenewalAndExpiresBothCookiePaths()
        {
            using var fixture = new AuthenticationFixture();
            var pair = fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), fixture.Request());
            var request = fixture.Request($"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n");
            fixture.Manager.Logout(request);
            Assert.Null(fixture.Manager.GetCurrentIdentity(request));
            Assert.Null(fixture.Manager.Refresh(pair.RefreshToken, fixture.Application));
            var response = new ResponseOK();
            fixture.Manager.ApplyAuthenticationCookies(request, response);
            Assert.All(response.Header.Cookies.Cast<System.Net.Cookie>(), x => Assert.True(x.Expires < DateTime.UtcNow));
            Assert.Equal(IdentityManager.RefreshPath, response.Header.Cookies[IdentityManager.RefreshCookieName].Path);
            Assert.Null(request.ExistingSession);
        }

        /// <summary>
        /// An expired access credential can revoke its grant but cannot regain authorization by reaching logout.
        /// </summary>
        [Fact]
        public void ExpiredAccessCanOnlyRevokeRenewal()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application;
            var pair = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), app);
            fixture.Clock.Now = pair.AccessTokenExpiresAt.AddSeconds(1);
            Assert.Null(fixture.Manager.ValidateAccessToken(pair.AccessToken, app));
            fixture.Manager.RevokeGrant(pair.AccessToken, app);
            Assert.Null(fixture.Manager.Refresh(pair.RefreshToken, app));
        }

        /// <summary>
        /// External policy mappings produce the same signed permission snapshot as local policy-bearing groups.
        /// </summary>
        [Fact]
        public void MappedExternalPoliciesCaptureEffectivePermissions()
        {
            using var fixture = new AuthenticationFixture();
            var identity = new Identity(Guid.NewGuid(), "external", policyNames: [typeof(TestIdentityPolicyA).FullName]);
            var pair = fixture.Manager.Login(identity, fixture.Request());
            var verified = fixture.Manager.ValidateAccessToken(pair.AccessToken, fixture.Application);
            Assert.Contains(typeof(TestIdentityPermissionC).FullName, verified.Permissions);
        }

        /// <summary>
        /// Provider discovery and removal follow the plugin lifecycle instead of leaving stale authentication sources.
        /// </summary>
        [Fact]
        public void ProviderLifecycleFollowsPluginRemoval()
        {
            using var fixture = new AuthenticationFixture();
            var providers = fixture.Hub.IdentityProviderManager.GetProviders(fixture.Application).ToArray();
            Assert.Contains(providers, x => x is MockIdentityProvider);
            ((WebPlugin.PluginManager)fixture.Hub.PluginManager).Remove(fixture.Application.PluginContext);
            Assert.Empty(fixture.Hub.IdentityProviderManager.GetProviders(fixture.Application));
        }
    }
}
