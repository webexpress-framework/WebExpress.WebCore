using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Reflection;
using System.Text;
using static WebExpress.WebCore.Test.Fixture.HealthTestFixture;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebHealt;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Verifies the public health contract through the HTTP pipeline and a real Kestrel listener.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHttpServerHealth
    {
        /// <summary>
        /// Keeps the global route independent of application prefixes without reserving unrelated paths.
        /// </summary>
        /// <param name="path">The requested route, including optional query parameters.</param>
        /// <param name="matches">Whether the route belongs to the health endpoint.</param>
        [Theory]
        [InlineData("/health", true)]
        [InlineData("/health/", true)]
        [InlineData("/health?probe=readiness", true)]
        [InlineData("/health/live", true)]
        [InlineData("/health/live/", true)]
        [InlineData("/health/live?probe=liveness", true)]
        [InlineData("/healthy", false)]
        [InlineData("/health/details", false)]
        [InlineData("/health/lively", false)]
        [InlineData("/app/health/live", false)]
        [InlineData("/server/health", false)]
        [InlineData("/app/health", false)]
        [InlineData("/", false)]
        public void Matches_ReservesOnlyGlobalHealthRoute(string path, bool matches)
        {
            // arrange
            var context = UnitTestFixture.CreateHttpContextMock($"GET {path} HTTP/1.1\r\n\r\n");

            // act
            var actual = HealthEndpoint.Matches(context);

            // validation
            Assert.Equal(matches, actual);
        }

        /// <summary>
        /// Keeps probes usable before the hub exists and avoids session or authentication dependencies.
        /// </summary>
        /// <param name="method">The supported probe method.</param>
        /// <returns>A task that completes after the startup response is validated.</returns>
        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        public async Task ProcessRequestAsync_MissingHub_ReturnsSanitized503(string method)
        {
            // arrange
            var previous = WebEx.ComponentHub;
            var hubField = typeof(WebEx).GetField("_componentHub", BindingFlags.Static | BindingFlags.NonPublic);
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock());
            var context = UnitTestFixture.CreateHttpContextMock($"{method} /health HTTP/1.1\r\nCookie:\r\n\r\n");
            var response = new HttpResponseFeature();
            using var output = new MemoryStream();
            context.Features.Set<IHttpResponseFeature>(response);
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));
            hubField.SetValue(null, null);

            try
            {
                // act
                await server.ProcessRequestAsync(context);
                var body = Encoding.UTF8.GetString(output.ToArray());

                // validation
                Assert.Equal(503, response.StatusCode);
                Assert.Equal("application/json; charset=utf-8", response.Headers.ContentType.ToString());
                Assert.Equal("no-store", response.Headers.CacheControl.ToString());
                Assert.False(response.Headers.ContainsKey("Set-Cookie"));
                Assert.Null(((RequestBase)context.Request).ExistingSession);
                Assert.Equal(method == "HEAD" ? string.Empty : UnhealthyBody, body);
            }
            finally
            {
                hubField.SetValue(null, previous);
            }
        }

        /// <summary>
        /// Prevents a framework failure from invoking diagnostic HTML status pages for health requests.
        /// </summary>
        /// <returns>A task that completes after the public response and private log are validated.</returns>
        [Fact]
        public async Task HandleAsync_ManagerThrows_DetailsStayInServerLog()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var request = UnitTestFixture.CreateRequestMock("GET /health HTTP/1.1\r\n\r\n");
            using var manager = new FailingHealthManager();

            // act
            var response = await HealthEndpoint.HandleAsync(request, manager, context.Log, TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(503, response.Status);
            Assert.Equal(UnhealthyBody, response.Content);
            Assert.Contains(context.Log.GetRecentEntries(), x => x.Message.Contains("private-manager-diagnostic"));
        }

        /// <summary>
        /// Separates the liveness path so it never runs application checks.
        /// </summary>
        /// <param name="path">The requested route.</param>
        /// <param name="liveness">Whether the route is the liveness probe.</param>
        [Theory]
        [InlineData("/health/live", true)]
        [InlineData("/health/live/?probe=liveness", true)]
        [InlineData("/health", false)]
        [InlineData("/health/", false)]
        public void IsLiveness_DistinguishesLivenessFromReadiness(string path, bool liveness)
        {
            // arrange
            var context = UnitTestFixture.CreateHttpContextMock($"GET {path} HTTP/1.1\r\n\r\n");

            // act
            var actual = HealthEndpoint.IsLiveness(context);

            // validation
            Assert.Equal(liveness, actual);
        }

        /// <summary>
        /// Answers the liveness probe from the framework state alone, so a failing dependency cannot restart the process.
        /// </summary>
        /// <returns>A task that completes after the liveness response is validated.</returns>
        [Fact]
        public async Task HandleAsync_Liveness_SkipsApplicationChecks()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var request = UnitTestFixture.CreateRequestMock("GET /health/live HTTP/1.1\r\n\r\n");
            using var manager = new FailingHealthManager { Live = true };

            // act
            var live = await HealthEndpoint.HandleAsync(request, manager, context.Log, TestContext.Current.CancellationToken, liveness: true);
            manager.Live = false;
            var dead = await HealthEndpoint.HandleAsync(request, manager, context.Log, TestContext.Current.CancellationToken, liveness: true);

            // validation
            Assert.Equal(200, live.Status);
            Assert.Equal("{\"status\":\"healthy\"}", live.Content);
            Assert.Equal(503, dead.Status);
            Assert.Equal(UnhealthyBody, dead.Content);
            Assert.DoesNotContain(context.Log.GetRecentEntries(), x => x.Message.Contains("private-manager-diagnostic"));
        }

        /// <summary>
        /// Keeps request parsing errors from exposing technical details on the reserved health path.
        /// </summary>
        /// <returns>A task that completes after the malformed probe response is validated.</returns>
        [Fact]
        public async Task ProcessRequestAsync_ParsingFailed_ReturnsSanitized503()
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock());
            var features = UnitTestFixture.CreateHttpContextMock("GET /health HTTP/1.1\r\nCookie:\r\n\r\n").Features;
            var context = new HttpExceptionContext(new InvalidOperationException("private-parser-diagnostic"), features);
            var response = new HttpResponseFeature();
            using var output = new MemoryStream();
            features.Set<IHttpResponseFeature>(response);
            features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));

            // act
            await server.ProcessRequestAsync(context);

            // validation
            Assert.Equal(503, response.StatusCode);
            Assert.Equal(UnhealthyBody, Encoding.UTF8.GetString(output.ToArray()));
            Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("private-parser-diagnostic"));
        }

        /// <summary>
        /// Proves that the production startup order serves fresh aggregate health through an actual listener.
        /// </summary>
        /// <returns>A task that completes after success, failure, recovery, method, and HEAD responses are validated.</returns>
        [Fact]
        public async Task Kestrel_GlobalHealth_ReportsDependencyFailuresAndRecovery()
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings
                {
                    Endpoints = [new EndpointSettings { Uri = "http://127.0.0.1:0/" }]
                }
            };
            using var hub = UnitTestFixture.CreateComponentHubMock(server.HttpServerContext);
            var failure = "none";
            var calls = 0;
            using var registration = Register(hub.HealthManager, new ApplicationContext { ApplicationId = "secret-application" },
                "secret-database", async token =>
                {
                    Interlocked.Increment(ref calls);
                    if (failure == "timeout")
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    }

                    return failure switch
                    {
                        "exception" => throw new InvalidOperationException("secret-connection-string"),
                        "result" => HealthCheckResult.Unhealthy("secret-connection-string"),
                        _ => HealthCheckResult.Healthy()
                    };
                }, TimeSpan.FromMilliseconds(100));

            try
            {
                Assert.False(server.IsRunning);
                Assert.True(server.Start());
                var kestrel = (IServer)typeof(HttpServer).GetProperty("Kestrel", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(server);
                var address = kestrel.Features.Get<IServerAddressesFeature>().Addresses.Single();
                using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };

                // act
                using var healthy = await client.GetAsync("/health?probe=readiness", TestContext.Current.CancellationToken);
                failure = "result";
                using var failed = await client.GetAsync("/health", TestContext.Current.CancellationToken);
                var callsBeforeLive = calls;
                using var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
                var callsAfterLive = calls;
                failure = "exception";
                using var exception = await client.GetAsync("/health/", TestContext.Current.CancellationToken);
                failure = "none";
                using var recovered = await client.GetAsync("/health", TestContext.Current.CancellationToken);
                using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/health"), TestContext.Current.CancellationToken);
                failure = "timeout";
                using var timeout = await client.GetAsync("/health", TestContext.Current.CancellationToken);
                var callsBeforePost = calls;
                using var post = await client.PostAsync("/health", new StringContent(string.Empty), TestContext.Current.CancellationToken);

                // validation
                Assert.True(server.IsRunning);
                Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
                Assert.Equal("{\"status\":\"healthy\"}", await healthy.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                foreach (var response in new[] { failed, exception, timeout })
                {
                    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                    Assert.Equal(UnhealthyBody, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                    Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
                    Assert.True(response.Headers.CacheControl.NoStore);
                    Assert.False(response.Headers.Contains("Set-Cookie"));
                }
                Assert.Equal(HttpStatusCode.OK, live.StatusCode);
                Assert.Equal(callsBeforeLive, callsAfterLive);
                Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
                Assert.Equal(HttpStatusCode.OK, head.StatusCode);
                Assert.Empty(await head.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
                Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
                Assert.Contains("GET", post.Content.Headers.Allow);
                Assert.Contains("HEAD", post.Content.Headers.Allow);
                Assert.Equal(callsBeforePost, calls);
                Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("secret-connection-string"));
            }
            finally
            {
                server.Stop();
            }

            Assert.False(server.IsRunning);
            Assert.False(await hub.HealthManager.CheckAsync(TestContext.Current.CancellationToken));
        }

        private const string UnhealthyBody = "{\"status\":\"unhealthy\",\"message\":\"One or more critical components are unavailable.\"}";

        /// <summary>
        /// Injects a framework failure without relying on application callback handling.
        /// </summary>
        private sealed class FailingHealthManager : IHealthManager
        {
            /// <summary>
            /// Completes the discovery contract without publishing test bindings.
            /// </summary>
            public event EventHandler<IHealthContext> AddHealth { add { } remove { } }

            /// <summary>
            /// Completes the removal contract without retaining event subscribers.
            /// </summary>
            public event EventHandler<IHealthContext> RemoveHealth { add { } remove { } }

            /// <summary>
            /// Keeps the test double independent of component discovery.
            /// </summary>
            public IEnumerable<IHealthContext> HealthChecks => [];

            /// <summary>
            /// Returns no bindings because this test double only models probe failures.
            /// </summary>
            /// <param name="applicationContext">The unused application owner.</param>
            /// <returns>An empty collection of component bindings.</returns>
            public IEnumerable<IHealthContext> GetHealthChecks(IApplicationContext applicationContext)
            {
                return [];
            }

            /// <summary>
            /// Simulates an unexpected framework failure containing private diagnostics.
            /// </summary>
            /// <param name="cancellationToken">The unused probe cancellation token.</param>
            /// <returns>A failed task containing the simulated framework error.</returns>
            public Task<bool> CheckAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromException<bool>(new InvalidOperationException("private-manager-diagnostic"));
            }

            /// <summary>
            /// Gets or sets the framework state the liveness probe reports.
            /// </summary>
            public bool Live { get; set; }

            /// <summary>
            /// Reports the configured framework state without touching the failing readiness path.
            /// </summary>
            /// <returns>The configured framework state.</returns>
            public bool CheckLiveness()
            {
                return Live;
            }

            /// <summary>
            /// Completes the manager contract without allocating resources in this test double.
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
