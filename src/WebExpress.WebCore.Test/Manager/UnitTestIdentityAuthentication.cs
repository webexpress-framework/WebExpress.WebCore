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
            var other = new IdentityTokenService(fixture.Settings, new FileIdentityTokenStore(fixture.Settings.TokenStorePath));
            var identity = other.ValidateAccessToken(pair.AccessToken, fixture.Application.ApplicationId);
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
        /// Signature, audience, issuer, expiration, and token purpose remain mandatory even when claims look plausible.
        /// </summary>
        [Fact]
        public void TokenValidationEnforcesEveryTrustBoundary()
        {
            using var fixture = new AuthenticationFixture();
            var identity = new Identity(Guid.NewGuid(), "alice", permissions: ["read"]);
            var app = fixture.Application.ApplicationId;
            var pair = fixture.Tokens.Issue(identity, app);
            var parts = pair.AccessToken.Split('.');
            parts[1] = Base64UrlEncoder.Encode(Base64UrlEncoder.Decode(parts[1]).Replace("alice", "admin"));
            Assert.Null(fixture.Tokens.ValidateAccessToken(string.Join('.', parts), app));
            Assert.Null(fixture.Tokens.ValidateAccessToken(pair.AccessToken, "another-application"));
            Assert.Null(fixture.Tokens.ValidateAccessToken(pair.RefreshToken, app));
            Assert.Null(fixture.Tokens.Refresh(pair.AccessToken, app));
            Assert.Null(fixture.Tokens.ValidateAccessToken("not.a.jwt", app));
            fixture.Settings.Issuer = "https://another-authority.test";
            var wrongIssuer = new IdentityTokenService(fixture.Settings, new FileIdentityTokenStore(fixture.Settings.TokenStorePath), fixture.Clock);
            Assert.Null(wrongIssuer.ValidateAccessToken(pair.AccessToken, app));
            fixture.Settings.Issuer = "https://webexpress.test";
            fixture.Settings.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var wrongKey = new IdentityTokenService(fixture.Settings, new FileIdentityTokenStore(fixture.Settings.TokenStorePath), fixture.Clock);
            Assert.Null(wrongKey.ValidateAccessToken(pair.AccessToken, app));
            fixture.Clock.Now = pair.AccessTokenExpiresAt.AddSeconds(1);
            Assert.Null(fixture.Tokens.ValidateAccessToken(pair.AccessToken, app));
            Assert.NotNull(fixture.Tokens.Refresh(pair.RefreshToken, app));
        }

        /// <summary>
        /// Refresh replay revokes its successor across instances and renewal never moves the absolute grant deadline.
        /// </summary>
        [Fact]
        public void RefreshRotatesOnceAndRetainsAbsoluteExpiry()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application.ApplicationId;
            var pair = fixture.Tokens.Issue(new Identity(Guid.NewGuid(), "alice"), app);
            fixture.Clock.Now += TimeSpan.FromMinutes(10);
            var next = fixture.Tokens.Refresh(pair.RefreshToken, app);
            Assert.NotEqual(pair.RefreshToken, next.RefreshToken);
            Assert.Equal(pair.RefreshTokenExpiresAt.ToUnixTimeSeconds(), next.RefreshTokenExpiresAt.ToUnixTimeSeconds());
            var other = new IdentityTokenService(fixture.Settings, new FileIdentityTokenStore(fixture.Settings.TokenStorePath), fixture.Clock);
            Assert.Null(other.Refresh(pair.RefreshToken, app));
            Assert.Null(fixture.Tokens.Refresh(next.RefreshToken, app));
            var fresh = fixture.Tokens.Issue(new Identity(Guid.NewGuid(), "bob"), app);
            fixture.Clock.Now = fresh.RefreshTokenExpiresAt.AddSeconds(1);
            Assert.Null(fixture.Tokens.Refresh(fresh.RefreshToken, app));
        }

        /// <summary>
        /// Exclusive marker creation permits only one winner when different nodes redeem the same refresh token.
        /// </summary>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Fact]
        public async Task ConcurrentRefreshHasOnlyOneWinner()
        {
            using var fixture = new AuthenticationFixture();
            var app = fixture.Application.ApplicationId;
            var pair = fixture.Tokens.Issue(new Identity(Guid.NewGuid(), "alice"), app);
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                new IdentityTokenService(fixture.Settings, new FileIdentityTokenStore(fixture.Settings.TokenStorePath), fixture.Clock)
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
            var app = fixture.Application.ApplicationId;
            var owner = new Identity(Guid.NewGuid(), "alice", roles: ["admin"], permissions: ["read", "write"], policyNames: ["admin-policy"]);
            var token = fixture.Tokens.CreatePersonalAccessToken(owner, app, TimeSpan.FromHours(1), ["read"]);
            var restricted = fixture.Tokens.ValidatePersonalAccessToken(token, app);
            Assert.Equal(["read"], restricted.Permissions);
            Assert.Empty(restricted.Roles);
            Assert.Empty(restricted.PolicyNames);
            Assert.Null(fixture.Tokens.ValidateAccessToken(token, app));
            Assert.Null(fixture.Tokens.Refresh(token, app));
            Assert.Throws<ArgumentException>(() => fixture.Tokens.CreatePersonalAccessToken(owner, app, TimeSpan.FromHours(1), ["delete"]));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Tokens.CreatePersonalAccessToken(owner, app, TimeSpan.Zero, ["read"]));
            Assert.True(fixture.Tokens.RevokePersonalAccessToken(token, app));
            var other = new IdentityTokenService(fixture.Settings, new FileIdentityTokenStore(fixture.Settings.TokenStorePath), fixture.Clock);
            Assert.Null(other.ValidatePersonalAccessToken(token, app));
            var expiring = fixture.Tokens.CreatePersonalAccessToken(owner, app, TimeSpan.FromSeconds(1), ["read"]);
            fixture.Clock.Now += TimeSpan.FromSeconds(2);
            Assert.Null(fixture.Tokens.ValidatePersonalAccessToken(expiring, app));
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
            Assert.Null(fixture.Tokens.Refresh(pair.RefreshToken, fixture.Application.ApplicationId));
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
            var app = fixture.Application.ApplicationId;
            var pair = fixture.Tokens.Issue(new Identity(Guid.NewGuid(), "alice"), app);
            fixture.Clock.Now = pair.AccessTokenExpiresAt.AddSeconds(1);
            Assert.Null(fixture.Tokens.ValidateAccessToken(pair.AccessToken, app));
            fixture.Tokens.RevokeGrant(pair.AccessToken, app);
            Assert.Null(fixture.Tokens.Refresh(pair.RefreshToken, app));
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
            var verified = fixture.Tokens.ValidateAccessToken(pair.AccessToken, fixture.Application.ApplicationId);
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
