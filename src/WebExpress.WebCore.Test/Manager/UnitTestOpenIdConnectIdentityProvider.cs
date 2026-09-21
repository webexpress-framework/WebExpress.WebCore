using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            var redirect = await provider.CreateChallengeAsync(fixture.Tokens, fixture.Application.ApplicationId);
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
            var identity = await provider.AuthenticateCallbackAsync(callback, fixture.Tokens);
            Assert.NotNull(identity);
            Assert.Equal("alice", identity.Name);
            Assert.Equal(["read", "write"], identity.Permissions.Order());
            Assert.DoesNotContain("unmapped", identity.Roles);
            Assert.Equal(challenge.GetPayloadValue<string>("verifier"), backchannel.Verifier);
            Assert.Equal(parameters["code_challenge"], Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(backchannel.Verifier))));
            var pair = fixture.Manager.Login(identity, callback);
            var authenticatedRequest = fixture.Request($"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n");
            Assert.Equal(identity.Id, fixture.Manager.GetCurrentIdentity(authenticatedRequest)?.Id);
            Assert.Null(await provider.AuthenticateCallbackAsync(callback, fixture.Tokens));
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
            var redirect = await provider.CreateChallengeAsync(fixture.Tokens, fixture.Application.ApplicationId);
            var parameters = QueryHelpers.ParseQuery(new Uri(redirect.Header.Location).Query);
            backchannel.Nonce = parameters["nonce"];
            var cookie = redirect.Header.Cookies[OpenIdConnectIdentityProvider.ChallengeCookieName];
            var state = failure == "state" ? "attacker-state" : parameters["state"].ToString();
            var callback = fixture.Request($"Cookie: {cookie.Name}={cookie.Value}\r\n", "GET", $"/api/auth/callback?state={state}&code=code");
            Assert.Null(await provider.AuthenticateCallbackAsync(callback, fixture.Tokens));
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
            Assert.Null(await provider.AuthenticateCallbackAsync(fixture.Request(path: "/api/auth/callback?code=stolen&state=stolen"), fixture.Tokens));
            Assert.Null(await provider.AuthenticateCallbackAsync(fixture.Request(path: "/api/auth/callback?id_token=jwt"), fixture.Tokens));
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
