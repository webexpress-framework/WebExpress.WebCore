using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Exercises the public authentication boundary, including cookie transport and browser-origin checks.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestAuthenticationEndpoint
    {
        /// <summary>
        /// Keeps HTTPS mandatory when a deployment has not explicitly enabled development HTTP.
        /// </summary>
        /// <returns>A task that completes after the default transport rejection has been verified.</returns>
        [Fact]
        public async Task HttpIsRejectedWithoutDevelopmentOverride()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            fixture.Server.Configuration["WebExpress:Authentication:RequireHttps"] = null;
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);

            // act
            var response = await endpoint.HandleAsync(fixture.Request("X-WebExpress-Auth: 1\r\n", "POST",
                "/api/auth/login", "{}", https: false));

            // validation
            Assert.Equal(400, response.Status);
            Assert.Contains("https_required", (string)response.Content);
            Assert.Empty(response.Header.Cookies.Cast<System.Net.Cookie>());
        }

        /// <summary>
        /// Allows development HTTP without weakening token validation, cookie isolation, rotation, or logout.
        /// </summary>
        /// <returns>A task that completes after the development authentication lifecycle has been verified.</returns>
        [Fact]
        public async Task DevelopmentHttpSupportsLoginRefreshAndLogout()
        {
            using var fixture = new AuthenticationFixture(requireHttps: false);
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);
            fixture.Hub.IdentityProviderManager.Register(new PasswordProvider(), fixture.Application);
            var login = await endpoint.HandleAsync(fixture.Request("X-WebExpress-Auth: 1\r\n", "POST",
                "/api/auth/login", "{\"username\":\"alice\",\"password\":\"correct\"}", https: false));

            Assert.Equal(200, login.Status);
            var access = login.Header.Cookies[IdentityManager.DevelopmentAccessCookieName];
            var refresh = login.Header.Cookies[IdentityManager.DevelopmentRefreshCookieName];
            Assert.NotNull(access);
            Assert.NotNull(refresh);
            Assert.True(access.HttpOnly && refresh.HttpOnly);
            Assert.False(access.Secure || refresh.Secure);
            Assert.Equal("/", access.Path);
            Assert.Equal(IdentityManager.RefreshPath, refresh.Path);
            var authenticatedRequest = fixture.Request($"Cookie: {access.Name}={access.Value}\r\n", https: false);
            Assert.Equal("alice", fixture.Manager.GetCurrentIdentity(authenticatedRequest)?.Name);

            fixture.Server.Configuration["WebExpress:Authentication:RequireHttps"] = "true";
            Assert.Null(fixture.Manager.GetCurrentIdentity(authenticatedRequest));
            fixture.Server.Configuration["WebExpress:Authentication:RequireHttps"] = "false";

            var renewed = await endpoint.HandleAsync(fixture.Request($"X-WebExpress-Auth: 1\r\nCookie: {refresh.Name}={refresh.Value}\r\n",
                "POST", IdentityManager.RefreshPath, https: false));
            Assert.Equal(200, renewed.Status);
            var nextAccess = renewed.Header.Cookies[access.Name];
            var nextRefresh = renewed.Header.Cookies[refresh.Name];
            Assert.NotEqual(refresh.Value, nextRefresh.Value);
            var logout = await endpoint.HandleAsync(fixture.Request($"X-WebExpress-Auth: 1\r\nCookie: {nextAccess.Name}={nextAccess.Value}\r\n",
                "POST", "/api/auth/logout", https: false));
            Assert.Equal(204, logout.Status);
            Assert.All(logout.Header.Cookies.Cast<System.Net.Cookie>(), cookie => Assert.True(cookie.Expires < DateTime.UtcNow));
            var revoked = await endpoint.HandleAsync(fixture.Request($"X-WebExpress-Auth: 1\r\nCookie: {nextRefresh.Name}={nextRefresh.Value}\r\n",
                "POST", IdentityManager.RefreshPath, https: false));
            Assert.Equal(401, revoked.Status);
        }

        /// <summary>
        /// The server answers the root login route before sitemap lookup and serializes protected cookies on the wire.
        /// </summary>
        /// <param name="hubAvailableAtConstruction">Whether the hub is already available when the server is constructed.</param>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RootLoginIsIntegratedIntoTheHttpPipeline(bool hubAvailableAtConstruction)
        {
            using var fixture = new AuthenticationFixture();
            fixture.Hub.IdentityProviderManager.Register(new PasswordProvider(), fixture.Application);
            const string body = "{\"username\":\"alice\",\"password\":\"correct\"}";
            var context = UnitTestFixture.CreateHttpContextMock($"POST /api/auth/login HTTP/1.1\r\nCookie:\r\nX-WebExpress-Auth: 1\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n{body}");
            typeof(WebMessage.RequestBase).GetProperty(nameof(WebMessage.RequestBase.Scheme))
                .SetValue(context.Request, global::WebExpress.WebCore.WebUri.UriScheme.Https);
            context.Request.Uri.Scheme = global::WebExpress.WebCore.WebUri.UriScheme.Https;
            var head = new Microsoft.AspNetCore.Http.Features.HttpResponseFeature();
            using var output = new MemoryStream();
            context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(head);
            context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>(
                new Microsoft.AspNetCore.Http.StreamResponseBodyFeature(output));
            var hubField = typeof(WebEx).GetField("_componentHub", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            HttpServer server;
            try
            {
                if (!hubAvailableAtConstruction) { hubField.SetValue(null, null); }
                server = new HttpServer(fixture.Server);
            }
            finally
            {
                hubField.SetValue(null, fixture.Hub);
            }
            await server.ProcessRequestAsync(context);
            Assert.Equal(200, head.StatusCode);
            Assert.Contains(IdentityManager.AccessCookieName, head.Headers.SetCookie.ToString());
            Assert.Contains("Path=/api/auth/refresh", head.Headers.SetCookie.ToString());
            Assert.Contains("SameSite=Lax", head.Headers.SetCookie.ToString());
            Assert.DoesNotContain("session=", head.Headers.SetCookie.ToString());
            Assert.True(JsonDocument.Parse(output.ToArray()).RootElement.GetProperty("authenticated").GetBoolean());
        }

        /// <summary>
        /// Local login, renewal, and logout use the same protected cookies and expose no credentials in JSON.
        /// </summary>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Fact]
        public async Task LocalLoginRefreshAndLogoutUseTokenCookies()
        {
            using var fixture = new AuthenticationFixture();
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);
            fixture.Hub.IdentityProviderManager.Register(new PasswordProvider(), fixture.Application);
            var request = fixture.Request("X-WebExpress-Auth: 1\r\n", "POST", "/api/auth/login", "{\"username\":\"alice\",\"password\":\"correct\"}");
            var login = await endpoint.HandleAsync(request);
            Assert.Equal(200, login.Status);
            Assert.True(JsonDocument.Parse((string)login.Content).RootElement.GetProperty("authenticated").GetBoolean());
            Assert.Null(request.ExistingSession);
            Assert.Equal(2, login.Header.Cookies.Count);
            var refresh = login.Header.Cookies[IdentityManager.RefreshCookieName].Value;
            var access = login.Header.Cookies[IdentityManager.AccessCookieName].Value;
            Assert.DoesNotContain(access, (string)login.Content);
            Assert.DoesNotContain(refresh, (string)login.Content);
            var renewed = await endpoint.HandleAsync(fixture.Request($"X-WebExpress-Auth: 1\r\nCookie: {IdentityManager.RefreshCookieName}={refresh}\r\n", "POST", IdentityManager.RefreshPath));
            Assert.Equal(200, renewed.Status);
            var rotated = renewed.Header.Cookies[IdentityManager.RefreshCookieName].Value;
            Assert.NotEqual(refresh, rotated);
            var logout = await endpoint.HandleAsync(fixture.Request($"X-WebExpress-Auth: 1\r\nCookie: {IdentityManager.RefreshCookieName}={rotated}\r\n", "DELETE", IdentityManager.RefreshPath));
            Assert.Equal(204, logout.Status);
            var revoked = await endpoint.HandleAsync(fixture.Request($"X-WebExpress-Auth: 1\r\nCookie: {IdentityManager.RefreshCookieName}={rotated}\r\n", "POST", IdentityManager.RefreshPath));
            Assert.Equal(401, revoked.Status);
        }

        /// <summary>
        /// Rejected credentials and browser requests cannot cause token issuance or leak password validation details.
        /// </summary>
        /// <param name="headers">The HTTP headers required for the boundary scenario.</param>
        /// <param name="method">The HTTP method used to exercise the endpoint contract.</param>
        /// <param name="body">The payload serialized or supplied for the authentication request.</param>
        /// <param name="status">The expected HTTP status for the rejected boundary input.</param>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Theory]
        [InlineData("", "POST", "{\"username\":\"alice\",\"password\":\"correct\"}", 403)]
        [InlineData("X-WebExpress-Auth: 1\r\nOrigin: https://other.test\r\n", "POST", "{}", 403)]
        [InlineData("X-WebExpress-Auth: 1\r\n", "GET", null, 405)]
        [InlineData("X-WebExpress-Auth: 1\r\n", "POST", "not-json", 400)]
        [InlineData("X-WebExpress-Auth: 1\r\n", "POST", "{\"username\":\"alice\",\"password\":\"wrong\"}", 401)]
        [InlineData("X-WebExpress-Auth: 1\r\n", "POST", "{\"username\":\"missing\",\"password\":\"correct\"}", 401)]
        public async Task InvalidLoginDoesNotIssueCredentials(string headers, string method, string body, int status)
        {
            using var fixture = new AuthenticationFixture();
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);
            fixture.Hub.IdentityProviderManager.Register(new PasswordProvider(), fixture.Application);
            var response = await endpoint.HandleAsync(fixture.Request(headers, method, "/api/auth/login", body));
            Assert.Equal(status, response.Status);
            Assert.Empty(response.Header.Cookies.Cast<System.Net.Cookie>());
            Assert.Equal("no-store", response.Header.CacheControl);
        }

        /// <summary>
        /// PAT issuance and revocation require a browser identity and preserve the requested permission subset.
        /// </summary>
        /// <returns>A task that completes after the asynchronous assertions have finished.</returns>
        [Fact]
        public async Task PersonalCredentialEndpointRequiresOwner()
        {
            using var fixture = new AuthenticationFixture();
            using var endpoint = new AuthenticationEndpoint(fixture.Hub, fixture.Server);
            var owner = new Identity(Guid.NewGuid(), "alice", permissions: ["read"]);
            var pair = fixture.Manager.Login(owner, fixture.Request());
            var headers = $"X-WebExpress-Auth: 1\r\nCookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n";
            var response = await endpoint.HandleAsync(fixture.Request(headers, "POST", "/api/auth/pat", "{\"lifetimeSeconds\":3600,\"permissions\":[\"read\"]}"));
            Assert.Equal(201, response.Status);
            var token = JsonDocument.Parse((string)response.Content).RootElement.GetProperty("token").GetString();
            Assert.Equal(owner.Id, fixture.Manager.GetCurrentIdentity(fixture.Request($"Authorization: Bearer {token}\r\n")).Id);
            var forbidden = await endpoint.HandleAsync(fixture.Request(headers, "POST", "/api/auth/pat", "{\"lifetimeSeconds\":3600,\"permissions\":[\"write\"]}"));
            Assert.Equal(400, forbidden.Status);
            var revoked = await endpoint.HandleAsync(fixture.Request(headers, "DELETE", "/api/auth/pat", JsonSerializer.Serialize(new { token })));
            Assert.Equal(204, revoked.Status);
            Assert.Null(fixture.Manager.GetCurrentIdentity(fixture.Request($"Authorization: Bearer {token}\r\n")));
        }

        /// <summary>
        /// Exercises the production local password verifier with an isolated user directory.
        /// </summary>
        private sealed class PasswordProvider : LocalIdentityProvider
        {
            private readonly MockIdentity _user;
            /// <summary>
            /// Creates a salted password verifier for the local authentication boundary tests.
            /// </summary>
            internal PasswordProvider()
            {
                var hash = new PasswordHasher<IIdentity>().HashPassword(null, "correct");
                _user = new MockIdentity(Guid.NewGuid(), "alice", "alice@example.test", hash);
            }
            /// <summary>
            /// Supplies a salted local password verifier to exercise the real provider validation path.
            /// </summary>
            /// <returns>The identities supplied by the local directory, or an empty collection for external providers.</returns>
            public override IEnumerable<IIdentity> GetIdentities() => [_user];
        }
    }
}
