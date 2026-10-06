using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebSetting;
using ApplicationContext = WebExpress.WebCore.WebApplication.ApplicationContext;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Verifies the shared entry point without changing application routing or authentication.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestRootEndpoint : IDisposable
    {
        private readonly IComponentHub _previousHub = WebEx.ComponentHub;
        private readonly ComponentHub _hub = UnitTestFixture.CreateAndRegisterComponentHubMock();

        /// <summary>
        /// Restores the process-wide hub so entry point scenarios cannot affect unrelated tests.
        /// </summary>
        public void Dispose()
        {
            _hub.IdentityProviderManager.Dispose();
            typeof(WebEx).GetField("_componentHub", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousHub);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Covers automatic selection, explicit selection, disabled redirects, and missing targets.
        /// </summary>
        /// <param name="count">The number of configured applications.</param>
        /// <param name="redirectEnabled">The optional administrator preference.</param>
        /// <param name="applicationId">The optional application identifier to select.</param>
        /// <param name="target">The expected redirect path, or null for the overview.</param>
        [Theory]
        [InlineData(0, null, null, null)]
        [InlineData(1, null, null, "/server/app0")]
        [InlineData(2, null, null, null)]
        [InlineData(1, false, null, null)]
        [InlineData(2, false, "app1", null)]
        [InlineData(2, true, "app1", "/server/app1")]
        [InlineData(2, true, " APP1 ", "/server/app1")]
        [InlineData(1, true, "unknown", null)]
        [InlineData(2, true, "https://example.com", null)]
        [InlineData(1, true, " ", "/server/app0")]
        public void EntryPointSelectsConfiguredBehavior(int count, bool? redirectEnabled, string applicationId, string target)
        {
            // arrange
            var applications = Enumerable.Range(0, count).Select(i => new ApplicationContext
            {
                ApplicationId = $"app{i}",
                ApplicationName = $"Application {i}",
                Route = new RouteEndpoint($"server/app{i}")
            }).ToArray();
            var settings = redirectEnabled.HasValue
                ? new RootSettings { RedirectEnabled = redirectEnabled.Value, ApplicationId = applicationId }
                : null;
            var request = UnitTestFixture.CreateRequestMock("GET / HTTP/1.1\r\nAccept-Language: en\r\n\r\n");

            // act
            var response = RootEndpoint.Handle(request, new RouteEndpoint("server"), applications, settings);

            // validation
            Assert.Equal(target is null ? 200 : 302, response.Status);
            Assert.Equal(target, response.Header.Location);
            Assert.Equal("no-store", response.Header.CacheControl);
            if (target is null)
            {
                var html = response.Content.ToString();
                Assert.Contains(count == 0 ? "No applications are available." : "Select an application", html);
                foreach (var application in applications)
                {
                    Assert.Contains(application.ApplicationName, html);
                    Assert.Contains($"href=\"{application.Route}\"", html);
                }
            }
        }

        /// <summary>
        /// Accepts both entry points and leaves application URLs, unknown routes, and unsafe methods alone.
        /// </summary>
        /// <param name="method">The method supplied by the client.</param>
        /// <param name="path">The requested path, including any query string.</param>
        /// <param name="handled">Whether the shared entry point owns this request.</param>
        [Theory]
        [InlineData("GET", "/", true)]
        [InlineData("GET", "/?next=https://example.com", true)]
        [InlineData("GET", "/server", true)]
        [InlineData("GET", "/server/", true)]
        [InlineData("HEAD", "/", true)]
        [InlineData("GET", "/server/app", false)]
        [InlineData("GET", "/server/missing", false)]
        [InlineData("GET", "/server-other", false)]
        [InlineData("POST", "/", false)]
        public void OnlyEntryPointNavigationIsHandled(string method, string path, bool handled)
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock($"{method} {path} HTTP/1.1\r\n\r\n");
            var application = new ApplicationContext { Route = new RouteEndpoint("server/app") };

            // act
            var response = RootEndpoint.Handle(request, new RouteEndpoint("server"), [application], null);

            // validation
            Assert.Equal(handled, response is not null);
            if (handled)
            {
                Assert.Equal("/server/app", response.Header.Location);
            }
        }

        /// <summary>
        /// Keeps an application mounted at an entry point reachable through its normal endpoint pipeline.
        /// </summary>
        /// <param name="path">The entry point already owned by an application.</param>
        [Theory]
        [InlineData("/")]
        [InlineData("/server")]
        public void ApplicationAtEntryPointKeepsItsRoute(string path)
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock($"GET {path} HTTP/1.1\r\n\r\n");
            var application = new ApplicationContext { Route = new RouteEndpoint(path) };

            // act
            var response = RootEndpoint.Handle(request, new RouteEndpoint("server"), [application], null);

            // validation
            Assert.Null(response);
        }

        /// <summary>
        /// Renders application branding as text and uses the requested language for the selector.
        /// </summary>
        [Fact]
        public void OverviewEncodesNamesAndUsesRequestCulture()
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock("GET / HTTP/1.1\r\nAccept-Language: de\r\n\r\n");
            var application = new ApplicationContext
            {
                ApplicationName = "<script>alert('test')</script> & Anwendung",
                Route = new RouteEndpoint("server/app")
            };

            // act
            var response = RootEndpoint.Handle(request, new RouteEndpoint("server"), [application], new RootSettings { RedirectEnabled = false });

            // validation
            var html = response.Content.ToString();
            Assert.Contains("lang=\"de\"", html);
            Assert.Contains("Anwendungen", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("&amp; Anwendung", html);
            Assert.DoesNotContain("<script>", html);
        }

        /// <summary>
        /// Exercises configured redirects and overviews through the full HTTP response pipeline.
        /// </summary>
        /// <param name="redirectEnabled">Whether the selected application should receive the root navigation.</param>
        /// <param name="method">The HTTP method used to verify body suppression for HEAD.</param>
        /// <returns>A task that completes after the emitted response has been checked.</returns>
        [Theory]
        [InlineData(true, "GET")]
        [InlineData(false, "GET")]
        [InlineData(false, "HEAD")]
        public async Task HttpPipelineUsesBoundSettingsAndPreservesApplicationEndpoints(bool redirectEnabled, string method)
        {
            // arrange
            var application = _hub.ApplicationManager.GetApplication<TestApplicationA>();
            var configuration = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes($$"""
                { "WebExpress": { "Root": { "RedirectEnabled": {{redirectEnabled.ToString().ToLowerInvariant()}}, "ApplicationId": "{{application.ApplicationId}}" } } }
                """))).Build();
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock()) { Settings = configuration.GetServerSettings() };
            _hub.SitemapManager.Refresh();
            var context = UnitTestFixture.CreateHttpContextMock($"{method} / HTTP/1.1\r\nCookie:\r\n\r\n");
            var headers = new HttpResponseFeature();
            using var output = new MemoryStream();
            context.Features.Set<IHttpResponseFeature>(headers);
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));

            // act
            await server.ProcessRequestAsync(context);

            // validation
            Assert.Equal(redirectEnabled ? 302 : 200, headers.StatusCode);
            Assert.Equal(redirectEnabled ? "/server/appa" : "", headers.Headers.Location.ToString());
            Assert.Equal("no-store", headers.Headers.CacheControl);
            Assert.NotEmpty(headers.Headers["Content-Security-Policy"].ToString());
            Assert.Empty(headers.Headers.SetCookie.ToString());
            var html = Encoding.UTF8.GetString(output.ToArray());
            if (!redirectEnabled && method == "GET")
            {
                Assert.Contains("href=\"/server/appa\"", html);
                Assert.Contains("href=\"/server/appb\"", html);
                Assert.Contains("href=\"/server\"", html);
                Assert.Equal("text/html; charset=utf-8", headers.Headers.ContentType);
            }
            else
            {
                Assert.Empty(html);
            }

            // act
            var direct = UnitTestFixture.CreateHttpContextMock("GET /server/appa/api/2/testrestapib HTTP/1.1\r\nCookie:\r\n\r\n");
            var directHeaders = new HttpResponseFeature();
            using var directOutput = new MemoryStream();
            direct.Features.Set<IHttpResponseFeature>(directHeaders);
            direct.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(directOutput));
            await server.ProcessRequestAsync(direct);

            // validation
            Assert.Equal(400, directHeaders.StatusCode);
            Assert.Empty(directHeaders.Headers.Location.ToString());
        }
    }
}
