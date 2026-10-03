using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebMetrics;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Verifies the public metrics contract through the HTTP pipeline and a real Kestrel listener.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHttpServerMetrics
    {
        /// <summary>
        /// Keeps the global route independent of application prefixes without reserving unrelated paths.
        /// </summary>
        /// <param name="path">The requested route, including optional query parameters.</param>
        /// <param name="matches">Whether the route belongs to the metrics endpoint.</param>
        [Theory]
        [InlineData("/metrics", true)]
        [InlineData("/metrics/", true)]
        [InlineData("/metrics?name=x", true)]
        [InlineData("/metrics/details", false)]
        [InlineData("/metricsx", false)]
        [InlineData("/app/metrics", false)]
        [InlineData("/", false)]
        public void Matches_ReservesOnlyGlobalMetricsRoute(string path, bool matches)
        {
            // arrange
            var context = UnitTestFixture.CreateHttpContextMock($"GET {path} HTTP/1.1\r\n\r\n");

            // act
            var actual = MetricsEndpoint.Matches(context);

            // validation
            Assert.Equal(matches, actual);
        }

        /// <summary>
        /// Requires the configured bearer token and compares it exactly.
        /// </summary>
        /// <param name="authorization">The authorization header sent, or empty for none.</param>
        /// <param name="status">The expected status code.</param>
        /// <returns>A task that completes after the response has been validated.</returns>
        [Theory]
        [InlineData("", 401)]
        [InlineData("Bearer wrong", 401)]
        [InlineData("Bearer scrape-secret-longer", 401)]
        [InlineData("Basic c2NyYXBlOnNjcmFwZS1zZWNyZXQ=", 401)]
        [InlineData("Bearer scrape-secret", 200)]
        [InlineData("bearer scrape-secret", 200)]
        public async Task HandleAsync_BearerToken_GuardsEndpoint(string authorization, int status)
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            var header = authorization.Length == 0 ? string.Empty : $"Authorization: {authorization}\r\n";
            var request = UnitTestFixture.CreateRequestMock($"GET /metrics HTTP/1.1\r\n{header}\r\n");

            // act
            var response = await MetricsEndpoint.HandleAsync(request, hub.MetricsManager,
                new MetricsSettings { BearerToken = "scrape-secret" }, null, TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(status, response.Status);
            Assert.Equal("text/plain; version=0.0.4; charset=utf-8", response.Header.ContentType);
            Assert.Equal("no-store", response.Header.CacheControl);
            if (status == 401)
            {
                Assert.Equal("Bearer realm=\"metrics\"", response.Header.CustomHeader["WWW-Authenticate"]);
                Assert.False(response.Header.WWWAuthenticate);
                Assert.DoesNotContain("webexpress_", response.Content as string);
            }
            else
            {
                Assert.Contains("# TYPE webexpress_info gauge", response.Content as string);
            }
        }

        /// <summary>
        /// Allows only reading methods and omits the body of a HEAD request.
        /// </summary>
        /// <param name="method">The request method.</param>
        /// <param name="status">The expected status code.</param>
        /// <returns>A task that completes after the response has been validated.</returns>
        [Theory]
        [InlineData("GET", 200)]
        [InlineData("HEAD", 200)]
        [InlineData("POST", 405)]
        [InlineData("DELETE", 405)]
        public async Task HandleAsync_Method_ServesOnlyReads(string method, int status)
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock($"{method} /metrics HTTP/1.1\r\n\r\n");

            // act
            var response = await MetricsEndpoint.HandleAsync(request, hub.MetricsManager, null, null, TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(status, response.Status);
            if (status == 405)
            {
                Assert.Equal("GET, HEAD", response.Header.CustomHeader["Allow"]);
            }
            else
            {
                Assert.Equal(method == "HEAD", response.Content is null);
            }
        }

        /// <summary>
        /// Answers 503 without details while the manager is missing or fails, keeping the cause in the log.
        /// </summary>
        /// <returns>A task that completes after both failure modes have been validated.</returns>
        [Fact]
        public async Task HandleAsync_ManagerUnavailable_Returns503AndLogs()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var request = UnitTestFixture.CreateRequestMock("GET /metrics HTTP/1.1\r\n\r\n");
            var hub = UnitTestFixture.CreateComponentHubMock(context);
            var disposed = hub.MetricsManager;
            disposed.Dispose();

            // act
            var missing = await MetricsEndpoint.HandleAsync(request, null, null, context.Log, TestContext.Current.CancellationToken);
            var failed = await MetricsEndpoint.HandleAsync(request, disposed, null, context.Log, TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(503, missing.Status);
            Assert.Equal(503, failed.Status);
            Assert.Equal("metrics unavailable\n", failed.Content);
            Assert.Contains(context.Log.GetRecentEntries(), x => x.Message.Contains("metrics manager is unavailable"));
            Assert.Contains(context.Log.GetRecentEntries(), x => x.Level == WebExpress.WebCore.WebLog.LogLevel.Exception);
        }

        /// <summary>
        /// Leaves the path to application routing unless the endpoint is switched on explicitly.
        /// </summary>
        /// <param name="configuration">Whether the settings, the metrics block, or an explicit switch is absent.</param>
        /// <returns>A task that completes after the routed response has been validated.</returns>
        [Theory]
        [InlineData("no-settings")]
        [InlineData("no-block")]
        [InlineData("empty-block")]
        [InlineData("disabled")]
        public async Task ProcessRequestAsync_NotEnabled_FallsThroughToRouting(string configuration)
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = configuration switch
                {
                    "no-settings" => null,
                    "no-block" => new HttpServerSettings(),
                    "empty-block" => new HttpServerSettings { Metrics = new MetricsSettings() },
                    _ => new HttpServerSettings { Metrics = new MetricsSettings { Enabled = false, BearerToken = "secret" } }
                }
            };
            var hub = UnitTestFixture.CreateComponentHubMock(server.HttpServerContext);
            var context = UnitTestFixture.CreateHttpContextMock("GET /metrics HTTP/1.1\r\n\r\n");
            var response = new HttpResponseFeature();
            using var output = new MemoryStream();
            context.Features.Set<IHttpResponseFeature>(response);
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));

            // act
            await server.ProcessRequestAsync(context);

            // validation
            Assert.Equal(404, response.StatusCode);
            Assert.DoesNotContain("version=0.0.4", response.Headers.ContentType.ToString());
            Assert.Equal(1, ((MetricsManager)hub.MetricsManager).Framework.Requests.GetValue("GET", "404"));
        }

        /// <summary>
        /// Proves through an actual listener that application traffic is counted, probes and scrapes
        /// are not, and the token protects the endpoint.
        /// </summary>
        /// <returns>A task that completes after the scrapes have been validated.</returns>
        [Fact]
        public async Task Kestrel_Metrics_CountsTrafficButNotProbes()
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings
                {
                    Endpoints = [new EndpointSettings { Uri = "http://127.0.0.1:0/" }],
                    Metrics = new MetricsSettings { Enabled = true, BearerToken = "scrape-secret" }
                }
            };
            using var hub = UnitTestFixture.CreateComponentHubMock(server.HttpServerContext);
            using var registration = MetricTestFixture.Register(hub.MetricsManager, new ApplicationContext { ApplicationId = "ldap-app" },
                "ldap", (collector, _) =>
                {
                    collector.Counter("ldap_requests_total", "LDAP requests.", 42, new MetricLabel("result", "success"));
                    return Task.CompletedTask;
                });

            try
            {
                Assert.True(server.Start());
                var kestrel = (IServer)typeof(HttpServer).GetProperty("Kestrel", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(server);
                var address = kestrel.Features.Get<IServerAddressesFeature>().Addresses.Single();
                using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };

                // act
                using var missing = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);
                using var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
                using var anonymous = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);
                using var scrapeRequest = new HttpRequestMessage(HttpMethod.Get, "/metrics");
                scrapeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "scrape-secret");
                using var scrape = await client.SendAsync(scrapeRequest, TestContext.Current.CancellationToken);
                var body = await scrape.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

                // validation
                Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
                Assert.Equal(HttpStatusCode.OK, scrape.StatusCode);
                Assert.Equal("text/plain", scrape.Content.Headers.ContentType.MediaType);
                Assert.True(scrape.Headers.CacheControl.NoStore);
                Assert.False(scrape.Headers.Contains("Set-Cookie"));
                Assert.Contains("webexpress_http_requests_total{method=\"GET\",code=\"404\"} 1\n", body);
                Assert.Single(Regex.Matches(body, "^webexpress_http_requests_total\\{", RegexOptions.Multiline));
                Assert.Contains("webexpress_http_request_duration_seconds_count 1\n", body);
                Assert.Contains("webexpress_http_requests_in_flight 0\n", body);
                Assert.Contains("ldap_requests_total{application=\"ldap-app\",result=\"success\"} 42\n", body);
                Assert.Contains("webexpress_metric_collector_up{application=\"ldap-app\",", body);
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
