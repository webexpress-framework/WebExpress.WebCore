using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSetting;
using WebExpress.WebCore.WebSocket;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Protects WebSocket upgrades when public authorities differ from the listening endpoint.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWebSocketHandshake
    {
        /// <summary>
        /// Keeps a port in the Host header from failing context creation before the handshake.
        /// </summary>
        /// <param name="host">The authority sent by the client.</param>
        /// <param name="scheme">The scheme of the incoming HTTP upgrade request.</param>
        /// <param name="localPort">The listening port, which may differ from the public port.</param>
        /// <param name="externalUri">The public base URI configured for generated links.</param>
        /// <param name="expectedHost">The host name expected after separating the public port.</param>
        [Theory]
        [InlineData("localhost:8080", "http", 8080, null, "localhost")]
        [InlineData("localhost:8080", "http", 8080, "http://localhost:8080/", "localhost")]
        [InlineData("localhost:8080", "http", 5000, "http://localhost:8080/", "localhost")]
        [InlineData("192.168.0.5:8080", "http", 5000, "http://192.168.0.5:8080/", "192.168.0.5")]
        [InlineData("[::1]:8080", "http", 5000, "http://[::1]:8080/", "[::1]")]
        [InlineData("[::1]", "http", 80, null, "[::1]")]
        [InlineData("example.org:8443", "https", 5001, "https://example.org:8443/", "example.org")]
        [InlineData("example.org", "http", 5000, "https://example.org/", "example.org")]
        public void CreateContext_WithPublicHost_PreservesWebSocketRequest(
            string host, string scheme, int localPort, string externalUri, string expectedHost)
        {
            // arrange
            const string target = "/kleenestar/webexpress.webapp/ws/messagequeue?domains=updates";
            const string key = "dGhlIHNhbXBsZSBub25jZQ==";
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock(externalUri: externalUri));
            var context = new DefaultHttpContext();
            context.TraceIdentifier = "websocket-handshake-test";
            context.Connection.LocalIpAddress = IPAddress.Loopback;
            context.Connection.LocalPort = localPort;
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Connection.RemotePort = 54321;
            context.Request.Method = "GET";
            context.Request.Scheme = scheme;
            context.Request.Protocol = "HTTP/1.1";
            context.Request.Host = new HostString(host);
            context.Request.QueryString = new QueryString("?domains=updates");
            context.Request.Headers.Upgrade = "websocket";
            context.Request.Headers.Connection = "Upgrade";
            context.Request.Headers.SecWebSocketKey = key;
            context.Request.Headers.SecWebSocketVersion = "13";
            context.Features.Get<IHttpRequestFeature>().RawTarget = target;

            // act
            var result = server.CreateContext(context.Features);

            // validation
            var socketContext = Assert.IsType<HttpWebSocketContext>(result);
            Assert.IsType<RequestWebSocket>(socketContext.Request);
            Assert.Equal(expectedHost, socketContext.Uri.Host);
            Assert.Equal(localPort, socketContext.Uri.Port);
            Assert.Equal(target, socketContext.Uri.PathAndQuery);
            Assert.Equal("updates", socketContext.Request.GetParameter("domains")?.Value);
            Assert.Equal(key, socketContext.WebSocketKey);
            Assert.Equal(scheme == "https", socketContext.IsSecureWebSocket);
            Assert.Same(server.HttpServerContext, socketContext.HttpServerContext);
            Assert.Equal(externalUri, socketContext.HttpServerContext.ExternalUri);
        }

        /// <summary>
        /// Verifies that a handshake without session cookie returns the id of the session the socket
        /// is bound to, so the page's later requests share it and session-scoped notifications reach
        /// the socket, while a handshake that already carries the id gets no second cookie.
        /// </summary>
        [Fact]
        public void BindSocketSession_WithoutCookie_IssuesTheSocketSession()
        {
            // arrange
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock()) { Settings = new HttpServerSettings() };
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            var fresh = UnitTestFixture.CreateHttpContextMock("GET /ws HTTP/1.1\r\nCookie: \r\n\r\n");
            fresh.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

            // act
            server.BindSocketSession(fresh);

            // validation
            var issued = fresh.Features.Get<IHttpResponseFeature>().Headers.SetCookie.ToString();
            var session = ((RequestBase)fresh.Request).ExistingSession;
            Assert.NotNull(session);
            Assert.StartsWith($"session={session.Id};", issued);
            Assert.Contains("HttpOnly", issued);

            // arrange: the browser now sends the cookie it received
            var returning = UnitTestFixture.CreateHttpContextMock($"GET /ws HTTP/1.1\r\nCookie: session={session.Id}\r\n\r\n");
            returning.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

            // act
            server.BindSocketSession(returning);

            // validation
            Assert.Equal(session.Id, ((RequestBase)returning.Request).ExistingSession.Id);
            Assert.Empty(returning.Features.Get<IHttpResponseFeature>().Headers.SetCookie.ToString());
        }

        /// <summary>
        /// Verifies a real upgrade with a public Host and Origin while Kestrel listens on another port.
        /// </summary>
        /// <returns>A task that completes after the public URI and WebSocket handshake have been verified.</returns>
        [Fact]
        public async Task ConnectAsync_WithExternalUriAndMappedPort_CompletesHandshake()
        {
            // arrange
            const string externalUri = "http://localhost:8080/";
            var previousHub = WebEx.ComponentHub;
            var hubField = typeof(WebEx).GetField("_componentHub", BindingFlags.Static | BindingFlags.NonPublic);
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock(externalUri: externalUri))
            {
                Settings = new HttpServerSettings { Endpoints = [new() { Uri = "http://127.0.0.1:0/" }] }
            };
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            using var client = new ClientWebSocket();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
                var socket = (SocketContext)componentHub.SocketManager.GetSockets<TestSocketA>(application).First();
                socket.SupportedSubProtocol = "wxmsg";
                componentHub.SitemapManager.Refresh();
                var publicUri = componentHub.SitemapManager.GetUri<TestSocketA>(application);
                Assert.True(server.Start());
                var kestrel = (IServer)typeof(HttpServer).GetProperty("Kestrel", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(server);
                var address = kestrel.Features.Get<IServerAddressesFeature>().Addresses.Single();
                var target = new UriBuilder(address) { Scheme = "ws", Path = socket.Route.ToString() }.Uri;
                client.Options.SetRequestHeader("Host", "localhost:8080");
                client.Options.SetRequestHeader("Origin", "http://localhost:8080");
                client.Options.AddSubProtocol("wxmsg");

                // act
                await client.ConnectAsync(target, timeout.Token);

                // validation
                Assert.Equal("http://localhost:8080/server/appa/testsocketa", publicUri.ToString());
                Assert.Equal(WebSocketState.Open, client.State);
                Assert.Equal("wxmsg", client.SubProtocol);
                await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test complete", timeout.Token);
            }
            finally
            {
                client.Abort();
                server.Stop();
                componentHub.IdentityProviderManager.Dispose();
                hubField.SetValue(null, previousHub);
            }
        }
    }
}
