using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Reflection;
using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebCluster;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Tests the parts of the http server that only matter with several instances behind a load
    /// balancer: the endpoint receiving messages from other instances and the drain delay that
    /// lets the load balancer stop routing before the listener closes.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHttpServerCluster
    {
        private static readonly byte[] Secret = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();

        /// <summary>
        /// Captures the signed body a transport would post.
        /// </summary>
        private sealed class CaptureHandler : HttpMessageHandler
        {
            internal byte[] Body;
            internal string Signature;

            /// <summary>
            /// Records the request.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>An empty success response.</returns>
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                Signature = request.Headers.GetValues(HttpClusterTransport.SignatureHeader).Single();

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        /// <summary>
        /// Tests that a signed message from another instance reaches the subscribers and that
        /// everything else is refused before any routing.
        /// </summary>
        /// <returns>A task that completes after the requests were processed.</returns>
        [Fact]
        public async Task BusEndpointAcceptsOnlySignedMessages()
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock()) { Settings = new HttpServerSettings() };
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            ((ClusterManager)hub.ClusterManager).Configure(new ClusterSettings
            {
                NodeId = "node-b",
                Peers = ["http://127.0.0.1:1/"],
                Secret = Convert.ToBase64String(Secret)
            });
            var received = new List<ClusterMessage>();
            hub.ClusterManager.Subscribe("popup", received.Add);

            var capture = new CaptureHandler();
            using var sender = new HttpClusterTransport("node-a", Secret, ["http://node-b/"], handler: capture);
            await sender.SendAsync("popup", Encoding.UTF8.GetBytes("hello"), TestContext.Current.CancellationToken);
            var body = Encoding.UTF8.GetString(capture.Body);

            // act
            var accepted = await SendAsync(server, $"POST /_cluster/bus HTTP/1.1\r\n{HttpClusterTransport.SignatureHeader}: {capture.Signature}\r\n\r\n{body}");
            var replayed = await SendAsync(server, $"POST /_cluster/bus HTTP/1.1\r\n{HttpClusterTransport.SignatureHeader}: {capture.Signature}\r\n\r\n{body}");
            var unsigned = await SendAsync(server, $"POST /_cluster/bus HTTP/1.1\r\n\r\n{body}");
            var wrongMethod = await SendAsync(server, "GET /_cluster/bus HTTP/1.1\r\n\r\n");

            // validation
            Assert.Equal(204, accepted);
            Assert.Equal(403, replayed);
            Assert.Equal(403, unsigned);
            Assert.Equal(405, wrongMethod);
            Assert.Equal("hello", Encoding.UTF8.GetString(Assert.Single(received).Payload));
        }

        /// <summary>
        /// Tests that the path is left to the applications while no transport is configured, so
        /// a single instance exposes no instance-to-instance endpoint at all.
        /// </summary>
        /// <returns>A task that completes after the request was processed.</returns>
        [Fact]
        public async Task BusEndpointAbsentWithoutPeers()
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock()) { Settings = new HttpServerSettings() };
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);

            // act
            var status = await SendAsync(server, "POST /_cluster/bus HTTP/1.1\r\n\r\n{}");

            // validation
            Assert.NotEqual(204, status);
            Assert.NotEqual(403, status);
        }

        /// <summary>
        /// Tests that with an internal listener the bus answers on that port only, the internal
        /// port exposes nothing but the bus, and the public port leaves the path to the applications.
        /// </summary>
        /// <returns>A task that completes after the requests were processed.</returns>
        [Fact]
        public async Task InternalListenerIsolatesTheBus()
        {
            // arrange: the request mock arrives on local port 8080
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings { Cluster = new ClusterSettings { Listen = "http://127.0.0.1:8080/" } }
            };
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            ((ClusterManager)hub.ClusterManager).Configure(new ClusterSettings
            {
                NodeId = "node-b",
                Peers = ["http://127.0.0.1:1/"],
                Secret = Convert.ToBase64String(Secret)
            });
            var capture = new CaptureHandler();
            using var sender = new HttpClusterTransport("node-a", Secret, ["http://node-b/"], handler: capture);
            await sender.SendAsync("popup", [1], TestContext.Current.CancellationToken);
            var body = Encoding.UTF8.GetString(capture.Body);

            // act
            var bus = await SendAsync(server, $"POST /_cluster/bus HTTP/1.1\r\n{HttpClusterTransport.SignatureHeader}: {capture.Signature}\r\n\r\n{body}");
            var health = await SendAsync(server, "GET /health HTTP/1.1\r\n\r\n");
            server.Settings = new HttpServerSettings { Cluster = new ClusterSettings { Listen = "http://127.0.0.1:8081/" } };
            var publicBus = await SendAsync(server, $"POST /_cluster/bus HTTP/1.1\r\n{HttpClusterTransport.SignatureHeader}: {capture.Signature}\r\n\r\n{body}");

            // validation
            Assert.Equal(204, bus);
            Assert.Equal(404, health);
            Assert.NotEqual(204, publicBus);
            Assert.NotEqual(403, publicBus);
        }

        /// <summary>
        /// Tests that an internal listener sharing a port with a public endpoint, or without a
        /// fixed port, is refused at startup.
        /// </summary>
        /// <param name="listen">The internal listener.</param>
        /// <param name="valid">Whether startup must accept it.</param>
        [Theory]
        [InlineData(null, true)]
        [InlineData("http://0.0.0.0:8081/", true)]
        [InlineData("http://0.0.0.0:8080/", false)]
        [InlineData("http://0.0.0.0:0/", false)]
        [InlineData("ftp://0.0.0.0:8081/", false)]
        public void InternalListenerIsValidated(string listen, bool valid)
        {
            var settings = new HttpServerSettings
            {
                Endpoints = [new EndpointSettings { Uri = "http://0.0.0.0:8080/" }],
                Cluster = new ClusterSettings { Listen = listen }
            };

            Assert.Equal(valid, Record.Exception(settings.ValidateCluster) is null);
        }

        /// <summary>
        /// Tests that during the shutdown delay the instance reports itself unready but alive and
        /// keeps answering the requests the load balancer still routes to it.
        /// </summary>
        /// <returns>A task that completes after the server stopped.</returns>
        [Fact]
        public async Task ShutdownDelayKeepsServingWhileUnready()
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings
                {
                    Endpoints = [new EndpointSettings { Uri = "http://127.0.0.1:0/" }],
                    ShutdownDelaySeconds = 1
                }
            };
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            hub.SitemapManager.Refresh();

            try
            {
                Assert.True(server.Start());
                using var client = CreateClient(server);

                // act
                var shutdown = server.StopAsync(TestContext.Current.CancellationToken);
                var readiness = await SendAsync(server, "GET /health HTTP/1.1\r\n\r\n");
                var liveness = await SendAsync(server, "GET /health/live HTTP/1.1\r\n\r\n");
                using var served = await client.GetAsync("/server/appa/api/2/testrestapib", TestContext.Current.CancellationToken);

                // validation
                Assert.Equal(503, readiness);
                Assert.Equal(200, liveness);
                Assert.NotEqual(HttpStatusCode.ServiceUnavailable, served.StatusCode);
                Assert.True(server.IsRunning);

                await shutdown.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                Assert.False(server.IsRunning);
            }
            finally
            {
                await server.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        /// <summary>
        /// Tests that a delay beyond the bound is refused at startup.
        /// </summary>
        /// <param name="seconds">The configured delay.</param>
        /// <param name="valid">Whether startup must accept it.</param>
        [Theory]
        [InlineData(0, true)]
        [InlineData(15, true)]
        [InlineData(-1, false)]
        [InlineData(3601, false)]
        public void ShutdownDelayIsValidated(int seconds, bool valid)
        {
            var settings = new HttpServerSettings { ShutdownDelaySeconds = seconds };

            Assert.Equal(valid, Record.Exception(settings.ValidateShutdown) is null);
        }

        /// <summary>
        /// Passes a raw request through the server pipeline.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="raw">The request in wire format.</param>
        /// <returns>The status code of the response.</returns>
        private static async Task<int> SendAsync(HttpServer server, string raw)
        {
            var context = UnitTestFixture.CreateHttpContextMock(raw);
            var response = new HttpResponseFeature();
            using var output = new MemoryStream();
            context.Features.Set<IHttpResponseFeature>(response);
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));

            await server.ProcessRequestAsync(context);

            return response.StatusCode;
        }

        /// <summary>
        /// Connects to the ephemeral listener of a started server.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <returns>A client with a bounded timeout.</returns>
        private static HttpClient CreateClient(HttpServer server)
        {
            var kestrel = (IServer)typeof(HttpServer).GetProperty("Kestrel", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(server);

            return new HttpClient
            {
                BaseAddress = new Uri(kestrel.Features.Get<IServerAddressesFeature>().Addresses.Single()),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }
    }
}
