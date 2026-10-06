using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Fixture
{
    /// <summary>
    /// Provides isolated authentication configuration and durable markers for regression tests.
    /// </summary>
    internal sealed class AuthenticationFixture : IDisposable
    {
        internal AuthenticationSettings Settings { get; } = new()
        {
            Issuer = "https://webexpress.test", Audience = "webexpress-tests",
            SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            TokenStorePath = Path.Combine(Path.GetTempPath(), "webexpress-auth-" + Guid.NewGuid().ToString("N"))
        };
        internal ComponentHub Hub { get; }
        internal IHttpServerContext Server { get; }
        internal IApplicationContext Application { get; }
        internal IdentityManager Manager => (IdentityManager)Hub.IdentityManager;
        internal TestClock Clock { get; } = new();

        /// <summary>
        /// Creates an isolated signing authority and durable token directory for each test.
        /// </summary>
        /// <param name="requireHttps">Whether the test retains the production transport requirement.</param>
        /// <param name="withTokenStorePath">Whether the deployment configures a location for the default file store.</param>
        internal AuthenticationFixture(bool requireHttps = true, bool withTokenStorePath = true)
        {
            Settings.RequireHttps = requireHttps;
            if (!withTokenStorePath) { Settings.TokenStorePath = null; }
            var configuration = Configuration();
            Server = UnitTestFixture.CreateHttpServerContextMock(configuration: configuration);
            Hub = UnitTestFixture.CreateComponentHubMock(Server);
            ((PluginManager)Hub.PluginManager).Register();
            Application = Hub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            configuration["WebExpress:Authentication:ApplicationId"] = Application.ApplicationId;
            Manager.Clock = Clock;
        }

        /// <summary>
        /// Simulates another server instance that shares the hub's token store but reads the current settings,
        /// so a changed issuer or signing key yields an independent signing authority.
        /// </summary>
        /// <returns>A separate identity manager driven by the fixture clock.</returns>
        internal IdentityManager Instance()
        {
            var server = UnitTestFixture.CreateHttpServerContextMock(configuration: Configuration());
            var manager = ComponentActivator.CreateInstance<IdentityManager>(typeof(IdentityManager), server, Hub);
            manager.Clock = Clock;
            return manager;
        }

        /// <summary>
        /// Projects the current settings into the configuration section read by the identity manager.
        /// </summary>
        /// <returns>The configuration containing the authentication section.</returns>
        private IConfigurationRoot Configuration()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["WebExpress:Authentication:Issuer"] = Settings.Issuer,
                ["WebExpress:Authentication:Audience"] = Settings.Audience,
                ["WebExpress:Authentication:SigningKey"] = Settings.SigningKey,
                ["WebExpress:Authentication:RequireHttps"] = Settings.RequireHttps.ToString(),
                ["WebExpress:Authentication:TokenStorePath"] = Settings.TokenStorePath
            }).Build();
        }

        /// <summary>
        /// Builds a request with explicit transport semantics without allocating application session state.
        /// </summary>
        /// <param name="headers">The HTTP headers required for the boundary scenario.</param>
        /// <param name="method">The HTTP method used to exercise the endpoint contract.</param>
        /// <param name="path">The request route or cookie path that constrains credential use.</param>
        /// <param name="body">The payload serialized or supplied for the authentication request.</param>
        /// <param name="https">Whether the request uses HTTPS rather than development HTTP.</param>
        /// <returns>The request configured for the isolated application and selected transport.</returns>
        internal RequestBase Request(string headers = "", string method = "GET", string path = "/", string body = null, bool https = true)
        {
            var content = $"{method} {path} HTTP/1.1\r\nCookie:\r\n{headers}";
            if (body is not null) { content += $"Content-Type: application/json\r\nContent-Length: {System.Text.Encoding.UTF8.GetByteCount(body)}\r\n"; }
            var request = (RequestBase)UnitTestFixture.CreateRequestMock(content + "\r\n" + body);
            request.ApplicationContext = Application;
            var scheme = https ? global::WebExpress.WebCore.WebUri.UriScheme.Https : global::WebExpress.WebCore.WebUri.UriScheme.Http;
            typeof(RequestBase).GetProperty(nameof(RequestBase.Scheme)).SetValue(request, scheme);
            request.Uri.Scheme = scheme;
            return request;
        }

        /// <summary>
        /// Removes only the isolated marker directory created by this fixture.
        /// </summary>
        public void Dispose()
        {
            Hub.IdentityProviderManager.Dispose();
            Hub.IdentityTokenStoreManager.Dispose();
            if (Directory.Exists(Settings.TokenStorePath)) { Directory.Delete(Settings.TokenStorePath, true); }
        }

        /// <summary>
        /// Makes signed token expiration deterministic without delaying tests.
        /// </summary>
        internal sealed class TestClock : TimeProvider
        {
            internal DateTimeOffset Now = DateTimeOffset.UtcNow;
            /// <summary>
            /// Allows expiry boundaries to be exercised without sleeping.
            /// </summary>
            /// <returns>The current simulated UTC time.</returns>
            public override DateTimeOffset GetUtcNow() => Now;
        }
    }
}
