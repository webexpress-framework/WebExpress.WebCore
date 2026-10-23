using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Exercises real discovery, JWKS, code exchange, and JWT validation over a deterministic backchannel.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestOpenIdConnectIdentityProvider
    {
        /// <summary>
        /// Preserves the initiating destination while issuing browser cookies through the actual authentication endpoint.
        /// </summary>
        /// <param name="browser">Whether the initiating request selects browser navigation.</param>
        /// <param name="rememberMe">Whether the initiating request selects persistent cookies.</param>
        /// <returns>A task that completes after the callback and protected-cookie assertions.</returns>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task EndpointCompletesAuthenticationWithoutTrustingCallbackDestinations(bool browser, bool rememberMe)
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            using var backchannel = new Backchannel();
            using var client = new HttpClient(backchannel);
            using var provider = new PluginIdentityProvider(Settings(fixture), client);
            fixture.Hub.IdentityProviderManager.Register(provider, fixture.Application);
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);
            var start = "/api/auth/authorize?provider=external&rememberMe=" + rememberMe.ToString().ToLowerInvariant() + (browser
                ? "&returnUrl=" + Uri.EscapeDataString("/workspace?view=mine#items") + "&failureUrl=%2Flogin" : "");
            var redirect = await endpoint.HandleAsync(fixture.Request(path: start));
            var parameters = QueryHelpers.ParseQuery(new Uri(redirect.Header.Location).Query);
            backchannel.Nonce = parameters["nonce"];
            var cookie = redirect.Header.Cookies[OpenIdConnectIdentityProvider.ChallengeCookieName];
            var callback = fixture.Request($"Cookie: {cookie.Name}={cookie.Value}\r\n", path:
                $"/api/auth/callback?provider=external&state={parameters["state"]}&code=code&returnUrl=https%3A%2F%2Fevil.test&failureUrl=%2Fevil&rememberMe={!rememberMe}");

            // act
            var response = await endpoint.HandleAsync(callback);

            // validation
            Assert.Equal(browser ? 302 : 200, response.Status);
            if (browser) { Assert.Equal("/workspace?view=mine#items", response.Header.Location); }
            else { Assert.True(JsonDocument.Parse((string)response.Content).RootElement.GetProperty("authenticated").GetBoolean()); }
            Assert.True(response.Header.Cookies[IdentityManager.AccessCookieName].HttpOnly);
            Assert.True(response.Header.Cookies[IdentityManager.RefreshCookieName].Secure);
            Assert.Equal(rememberMe, response.Header.Cookies[IdentityManager.AccessCookieName].Expires != DateTime.MinValue);
            Assert.Equal(rememberMe, response.Header.Cookies[IdentityManager.RefreshCookieName].Expires != DateTime.MinValue);
            Assert.True(response.Header.Cookies[cookie.Name].Expires < DateTime.UtcNow);
            Assert.Equal("no-store", response.Header.CacheControl);
            Assert.Equal(1, backchannel.Exchanges);
            Assert.Equal("alice", fixture.Manager.GetCurrentIdentity(fixture.Request(
                $"Cookie: {IdentityManager.AccessCookieName}={response.Header.Cookies[IdentityManager.AccessCookieName].Value}\r\n"))?.Name);
            Assert.Equal(browser ? 302 : 401, (await endpoint.HandleAsync(callback)).Status);
            Assert.Equal(1, backchannel.Exchanges);
        }

        /// <summary>
        /// Returns cancellation, invalid state, and unavailable providers to the signed login destination without credentials.
        /// </summary>
        /// <param name="failure">The failure injected after authorization starts.</param>
        /// <returns>A task that completes after failure navigation has been checked.</returns>
        [Theory]
        [InlineData("cancel")]
        [InlineData("state")]
        [InlineData("unavailable")]
        [InlineData("nonce")]
        public async Task BrowserFailuresReturnToTheBoundLoginPage(string failure)
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            using var backchannel = new Backchannel();
            using var client = new HttpClient(backchannel);
            using var provider = new PluginIdentityProvider(Settings(fixture), client);
            fixture.Hub.IdentityProviderManager.Register(provider, fixture.Application);
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);
            var redirect = await endpoint.HandleAsync(fixture.Request(path:
                "/api/auth/authorize?provider=external&returnUrl=%2Fworkspace&failureUrl=%2Flogin%3Flang%3Dde%23form"));
            var parameters = QueryHelpers.ParseQuery(new Uri(redirect.Header.Location).Query);
            backchannel.Nonce = parameters["nonce"];
            backchannel.Failure = failure;
            var cookie = redirect.Header.Cookies[OpenIdConnectIdentityProvider.ChallengeCookieName];
            var state = failure == "state" ? "wrong" : parameters["state"].ToString();
            var result = failure == "cancel" ? "error=access_denied&error_description=untrusted" : "code=code";

            // act
            var response = await endpoint.HandleAsync(fixture.Request($"Cookie: {cookie.Name}={cookie.Value}\r\n", path:
                $"/api/auth/callback?provider=external&state={state}&{result}&failureUrl=https%3A%2F%2Fevil.test"));

            // validation
            Assert.Equal(302, response.Status);
            Assert.Equal("/login?lang=de&wx-auth-error=" + (failure == "unavailable"
                ? "identity_provider_unavailable" : "authentication_failed") + "#form", response.Header.Location);
            Assert.Null(response.Header.Cookies[IdentityManager.AccessCookieName]);
            Assert.True(response.Header.Cookies[cookie.Name].Expires < DateTime.UtcNow);
        }

        /// <summary>
        /// Rejects external or ambiguous navigation before initiating discovery or issuing correlation cookies.
        /// </summary>
        /// <param name="destination">The untrusted destination submitted at the authorization boundary.</param>
        /// <returns>A task that completes after the boundary response has been checked.</returns>
        [Theory]
        [InlineData("https://evil.test")]
        [InlineData("//evil.test")]
        [InlineData("/\\evil.test")]
        [InlineData("/%2fevil.test")]
        [InlineData("/ok%0d%0aLocation:evil")]
        [InlineData("relative")]
        public async Task AuthorizationRejectsUnsafeDestinations(string destination)
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);

            // act
            var response = await endpoint.HandleAsync(fixture.Request(path:
                "/api/auth/authorize?provider=external&returnUrl=" + Uri.EscapeDataString(destination)));

            // validation
            Assert.Equal(400, response.Status);
            Assert.Contains("invalid_redirect", (string)response.Content);
            Assert.Null(response.Header.Location);
            Assert.Empty(response.Header.Cookies.Cast<Cookie>());
        }

        /// <summary>
        /// Keeps missing browser correlation from turning callback query parameters into trusted navigation.
        /// </summary>
        /// <returns>A task that completes after the uncorrelated callback has been checked.</returns>
        [Fact]
        public async Task CallbackWithoutCorrelationDoesNotRedirect()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            using var client = new HttpClient(new Backchannel());
            using var provider = new PluginIdentityProvider(Settings(fixture), client);
            fixture.Hub.IdentityProviderManager.Register(provider, fixture.Application);
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);

            // act
            var response = await endpoint.HandleAsync(fixture.Request(path:
                "/api/auth/callback?provider=external&code=stolen&returnUrl=%2Fworkspace&failureUrl=%2Flogin"));

            // validation
            Assert.Equal(401, response.Status);
            Assert.Null(response.Header.Location);
            Assert.False(response.Header.WWWAuthenticate);
        }

        /// <summary>
        /// A plugin can map its verified external roles into the common identity without changing protocol validation.
        /// </summary>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Fact]
        public async Task CodeFlowUsesPkceAndProducesCommonIdentity()
        {
            using var fixture = new AuthenticationFixture();
            using var backchannel = new Backchannel();
            using var client = new HttpClient(backchannel);
            using var provider = new PluginIdentityProvider(Settings(fixture), client);
            var redirect = await provider.CreateChallengeAsync(fixture.Manager, fixture.Application);
            var parameters = QueryHelpers.ParseQuery(new Uri(redirect.Header.Location).Query);
            Assert.Equal("code", parameters["response_type"]);
            Assert.Equal("S256", parameters["code_challenge_method"]);
            Assert.False(parameters.ContainsKey("code_verifier"));
            var cookie = redirect.Header.Cookies[OpenIdConnectIdentityProvider.ChallengeCookieName];
            Assert.True(cookie.Secure && cookie.HttpOnly);
            var challenge = new JsonWebToken(cookie.Value);
            backchannel.Nonce = parameters["nonce"];
            var callback = fixture.Request($"Cookie: {cookie.Name}={cookie.Value}\r\n", "GET",
                $"/api/auth/callback?state={parameters["state"]}&code=single-use-code");
            var identity = await provider.AuthenticateCallbackAsync(callback, fixture.Manager);
            Assert.NotNull(identity);
            Assert.Equal("alice", identity.Name);
            Assert.Equal(["read", "write"], identity.Permissions.Order());
            Assert.DoesNotContain("unmapped", identity.Roles);
            Assert.Equal(challenge.GetPayloadValue<string>("verifier"), backchannel.Verifier);
            Assert.Equal(parameters["code_challenge"], Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(backchannel.Verifier))));
            var pair = fixture.Manager.Login(identity, callback);
            var authenticatedRequest = fixture.Request($"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n");
            Assert.Equal(identity.Id, fixture.Manager.GetCurrentIdentity(authenticatedRequest)?.Id);
            Assert.Null(await provider.AuthenticateCallbackAsync(callback, fixture.Manager));
            Assert.Equal(1, backchannel.Exchanges);
        }

        /// <summary>
        /// Invalid state is rejected before code exchange; untrusted signatures and claims never reach local issuance.
        /// </summary>
        /// <param name="failure">The trust boundary deliberately violated by the simulated external response.</param>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Theory]
        [InlineData("state")]
        [InlineData("nonce")]
        [InlineData("issuer")]
        [InlineData("audience")]
        [InlineData("signature")]
        [InlineData("expired")]
        [InlineData("azp")]
        [InlineData("unsigned")]
        public async Task InvalidExternalAuthenticationIsRejected(string failure)
        {
            using var fixture = new AuthenticationFixture();
            using var backchannel = new Backchannel { Failure = failure };
            using var client = new HttpClient(backchannel);
            using var provider = new PluginIdentityProvider(Settings(fixture), client);
            var redirect = await provider.CreateChallengeAsync(fixture.Manager, fixture.Application);
            var parameters = QueryHelpers.ParseQuery(new Uri(redirect.Header.Location).Query);
            backchannel.Nonce = parameters["nonce"];
            var cookie = redirect.Header.Cookies[OpenIdConnectIdentityProvider.ChallengeCookieName];
            var state = failure == "state" ? "attacker-state" : parameters["state"].ToString();
            var callback = fixture.Request($"Cookie: {cookie.Name}={cookie.Value}\r\n", "GET", $"/api/auth/callback?state={state}&code=code");
            Assert.Null(await provider.AuthenticateCallbackAsync(callback, fixture.Manager));
            Assert.Equal(failure == "state" ? 0 : 1, backchannel.Exchanges);
        }

        /// <summary>
        /// A callback cannot supply a bare ID token or exchange a code without the initiating browser cookie.
        /// </summary>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Fact]
        public async Task MissingBrowserCorrelationNeverCallsTokenEndpoint()
        {
            using var fixture = new AuthenticationFixture();
            using var backchannel = new Backchannel();
            using var client = new HttpClient(backchannel);
            using var provider = new PluginIdentityProvider(Settings(fixture), client);
            Assert.Null(await provider.AuthenticateCallbackAsync(fixture.Request(path: "/api/auth/callback?code=stolen&state=stolen"), fixture.Manager));
            Assert.Null(await provider.AuthenticateCallbackAsync(fixture.Request(path: "/api/auth/callback?id_token=jwt"), fixture.Manager));
            Assert.Equal(0, backchannel.Exchanges);
        }

        /// <summary>
        /// Binds the simulated external provider to the application under test.
        /// </summary>
        /// <param name="fixture">The isolated authentication context used by the test.</param>
        /// <returns>The authority configuration bound to the application under test.</returns>
        private static OpenIdConnectSettings Settings(AuthenticationFixture fixture) => new()
        {
            ProviderId = "external", Authority = "https://identity.test/realm", ClientId = "webexpress",
            RedirectUri = $"https://webexpress.test/api/auth/callback?application={fixture.Application.ApplicationId}&provider=external",
            RolePermissions = new() { ["reader"] = ["read"], ["editor"] = ["write"] }
        };

        /// <summary>
        /// Demonstrates provider extension in a plugin without a vendor implementation in WebCore.
        /// </summary>
        private sealed class PluginIdentityProvider : OpenIdConnectIdentityProvider
        {
            /// <summary>
            /// Reuses the generic code flow with plugin-specific configuration.
            /// </summary>
            /// <param name="settings">The authority and role mappings trusted by this plugin.</param>
            /// <param name="client">The controlled transport for the protocol test.</param>
            internal PluginIdentityProvider(OpenIdConnectSettings settings, HttpClient client) : base(settings, client) { }

            /// <summary>
            /// Translates the plugin's role claim after the common pipeline has validated the token.
            /// </summary>
            /// <param name="token">The verified external ID token supplied by the base provider.</param>
            /// <returns>The external role labels used by the configured local authorization mappings.</returns>
            protected override IEnumerable<string> ReadRoles(JsonWebToken token)
            {
                return token.TryGetPayloadValue<string[]>("plugin_roles", out var roles) ? roles : [];
            }
        }
        /// <summary>
        /// Simulates an external authority while retaining real cryptographic token validation.
        /// </summary>
        private sealed class Backchannel : HttpMessageHandler
        {
            private readonly RSA _rsa = RSA.Create(2048);
            internal string Nonce;
            internal string Failure;
            internal string Verifier;
            internal int Exchanges;

            /// <summary>
            /// Supplies protocol documents and a freshly signed ID token while preserving real cryptographic validation.
            /// </summary>
            /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
            /// <param name="cancellationToken">The cancellation token governing the simulated backchannel operation.</param>
            /// <returns>The simulated discovery, signing-key, or token response.</returns>
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Failure == "unavailable") { throw new HttpRequestException("Provider unavailable."); }
                object payload;
                if (request.RequestUri.AbsolutePath.EndsWith("openid-configuration"))
                {
                    payload = new
                    {
                        issuer = "https://identity.test/realm", authorization_endpoint = "https://identity.test/authorize",
                        token_endpoint = "https://identity.test/token", jwks_uri = "https://identity.test/keys"
                    };
                }
                else if (request.RequestUri.AbsolutePath == "/keys")
                {
                    var parameters = _rsa.ExportParameters(false);
                    payload = new { keys = new[] { new { kty = "RSA", kid = "test-key", use = "sig", alg = "RS256",
                        n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) } } };
                }
                else
                {
                    Assert.Equal("/token", request.RequestUri.AbsolutePath);
                    Assert.Equal(HttpMethod.Post, request.Method);
                    var form = QueryHelpers.ParseQuery(await request.Content.ReadAsStringAsync(cancellationToken));
                    Assert.Equal("authorization_code", form["grant_type"]);
                    Verifier = form["code_verifier"];
                    Exchanges++;
                    using var wrongKey = RSA.Create(2048);
                    var key = new RsaSecurityKey(Failure == "signature" ? wrongKey : _rsa) { KeyId = "test-key" };
                    var descriptor = new SecurityTokenDescriptor
                    {
                        Issuer = Failure == "issuer" ? "https://attacker.test" : "https://identity.test/realm",
                        Audience = Failure == "audience" ? "other-client" : "webexpress",
                        IssuedAt = DateTime.UtcNow.AddMinutes(-5), NotBefore = DateTime.UtcNow.AddMinutes(-5),
                        Expires = Failure == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
                        SigningCredentials = Failure == "unsigned" ? null : new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
                        Claims = new Dictionary<string, object>
                        {
                            ["sub"] = "external-user", ["preferred_username"] = "alice",
                            ["nonce"] = Failure == "nonce" ? "attacker-nonce" : Nonce,
                            ["azp"] = Failure == "azp" ? "attacker-client" : "webexpress",
                            ["plugin_roles"] = new[] { "reader", "editor" },
                            ["roles"] = new[] { "unmapped" }
                        }
                    };
                    payload = new { id_token = new JsonWebTokenHandler().CreateToken(descriptor) };
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            }

            /// <summary>
            /// Releases test signing material after each independent authorization flow.
            /// </summary>
            /// <param name="disposing">A value indicating whether managed test resources should be released.</param>
            protected override void Dispose(bool disposing)
            {
                if (disposing) { _rsa.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }
}
