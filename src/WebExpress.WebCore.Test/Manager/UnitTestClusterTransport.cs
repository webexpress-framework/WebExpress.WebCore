using System.Net;
using System.Text;
using WebExpress.WebCore.WebCluster;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests the http transport between instances. The receiving endpoint sits on the public
    /// listener, so most tests are about what it must refuse.
    /// </summary>
    public sealed class UnitTestClusterTransport
    {
        private static readonly byte[] Secret = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();

        /// <summary>
        /// Captures what a transport posts and optionally hands it to a receiving transport, as
        /// the endpoint of a peer would.
        /// </summary>
        private sealed class LoopbackHandler : HttpMessageHandler
        {
            internal readonly List<(Uri Uri, byte[] Body, string Signature)> Posted = [];
            internal HttpClusterTransport Target;

            /// <summary>
            /// Records the request and delivers it.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>204 when the target accepted the message, 403 otherwise.</returns>
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                var signature = request.Headers.GetValues(HttpClusterTransport.SignatureHeader).Single();

                lock (Posted)
                {
                    Posted.Add((request.RequestUri, body, signature));
                }

                var accepted = Target?.Receive(body, signature) ?? true;

                return new HttpResponseMessage(accepted ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden);
            }
        }

        /// <summary>
        /// Moves time on demand.
        /// </summary>
        private sealed class TestClock : TimeProvider
        {
            internal DateTimeOffset Now = DateTimeOffset.UtcNow;

            /// <summary>
            /// Returns the simulated time.
            /// </summary>
            /// <returns>The current simulated UTC time.</returns>
            public override DateTimeOffset GetUtcNow() => Now;
        }

        /// <summary>
        /// Tests that a message reaches the subscriber of the receiving instance with topic,
        /// origin and payload intact.
        /// </summary>
        [Fact]
        public async Task DeliversToPeer()
        {
            var handler = new LoopbackHandler();
            var sender = new HttpClusterTransport("node-a", Secret, ["http://peer:8080/"], handler: handler);
            var receiver = new HttpClusterTransport("node-b", Secret, ["http://peer:8080/"], handler: new LoopbackHandler());
            var received = new List<ClusterMessage>();
            receiver.Received += (_, message) => received.Add(message);
            handler.Target = receiver;

            await sender.SendAsync("topic", Encoding.UTF8.GetBytes("hello"), TestContext.Current.CancellationToken);

            var message = Assert.Single(received);
            Assert.Equal("topic", message.Topic);
            Assert.Equal("node-a", message.Origin);
            Assert.Equal("hello", Encoding.UTF8.GetString(message.Payload));
            Assert.InRange((DateTimeOffset.UtcNow - message.Sent.Value).TotalSeconds, 0, 5);
            Assert.Equal(new Uri("http://peer:8080/_cluster/bus"), handler.Posted.Single().Uri);
        }

        /// <summary>
        /// Tests that a message signed with another secret is refused.
        /// </summary>
        [Fact]
        public async Task RefusesForeignSecret()
        {
            var handler = new LoopbackHandler();
            var sender = new HttpClusterTransport("node-a", [.. Secret.Reverse()], ["http://peer/"], handler: handler);
            var receiver = new HttpClusterTransport("node-b", Secret, [], handler: new LoopbackHandler());
            var received = 0;
            receiver.Received += (_, _) => received++;
            handler.Target = receiver;

            await sender.SendAsync("topic", [1], TestContext.Current.CancellationToken);

            Assert.Equal(0, received);
        }

        /// <summary>
        /// Tests that changing a single byte of a signed body breaks the signature.
        /// </summary>
        [Fact]
        public async Task RefusesTamperedBody()
        {
            var handler = new LoopbackHandler();
            var sender = new HttpClusterTransport("node-a", Secret, ["http://peer/"], handler: handler);
            var receiver = new HttpClusterTransport("node-b", Secret, [], handler: new LoopbackHandler());

            await sender.SendAsync("topic", Encoding.UTF8.GetBytes("popup"), TestContext.Current.CancellationToken);

            var (_, body, signature) = handler.Posted.Single();
            body[^3] ^= 1;

            Assert.False(receiver.Receive(body, signature));
            Assert.False(receiver.Receive(body, null));
            Assert.False(receiver.Receive(body, "not base64"));
        }

        /// <summary>
        /// Tests that a captured message is accepted once and refused when replayed.
        /// </summary>
        [Fact]
        public async Task RefusesReplay()
        {
            var handler = new LoopbackHandler();
            var sender = new HttpClusterTransport("node-a", Secret, ["http://peer/"], handler: handler);
            var receiver = new HttpClusterTransport("node-b", Secret, [], handler: new LoopbackHandler());

            await sender.SendAsync("topic", [1], TestContext.Current.CancellationToken);

            var (_, body, signature) = handler.Posted.Single();

            Assert.True(receiver.Receive(body, signature));
            Assert.False(receiver.Receive(body, signature));
        }

        /// <summary>
        /// Tests that a message older than the replay window is refused, so a captured message
        /// cannot be replayed to an instance that never saw it - e.g. one started later.
        /// </summary>
        [Fact]
        public async Task RefusesStaleMessage()
        {
            var clock = new TestClock();
            var handler = new LoopbackHandler();
            var sender = new HttpClusterTransport("node-a", Secret, ["http://peer/"], handler: handler, timeProvider: clock);
            var receiver = new HttpClusterTransport("node-b", Secret, [], handler: new LoopbackHandler(), timeProvider: clock);

            await sender.SendAsync("topic", [1], TestContext.Current.CancellationToken);
            clock.Now += HttpClusterTransport.ReplayWindow + TimeSpan.FromSeconds(1);

            var (_, body, signature) = handler.Posted.Single();

            Assert.False(receiver.Receive(body, signature));
        }

        /// <summary>
        /// Tests that an instance drops the echo of its own message, which a dns entry listing
        /// every instance produces.
        /// </summary>
        [Fact]
        public async Task DropsOwnEcho()
        {
            var handler = new LoopbackHandler();
            var transport = new HttpClusterTransport("node-a", Secret, ["http://self/"], handler: handler);
            var received = 0;
            transport.Received += (_, _) => received++;
            handler.Target = transport;

            await transport.SendAsync("topic", [1], TestContext.Current.CancellationToken);

            Assert.Equal(0, received);
        }

        /// <summary>
        /// Tests that a dns entry expands to every address it resolves to.
        /// </summary>
        [Fact]
        public async Task ResolvesDnsPeers()
        {
            var handler = new LoopbackHandler();
            var transport = new HttpClusterTransport("node-a", Secret, ["dns://webexpress-headless:8080"], handler: handler,
                resolve: _ => Task.FromResult(new[] { IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.2"), IPAddress.Parse("fd00::1") }));

            await transport.SendAsync("topic", [1], TestContext.Current.CancellationToken);

            Assert.Equal
            (
                ["http://10.0.0.1:8080/_cluster/bus", "http://10.0.0.2:8080/_cluster/bus", "http://[fd00::1]:8080/_cluster/bus"],
                handler.Posted.Select(x => x.Uri.ToString()).Order(StringComparer.Ordinal)
            );
        }

        /// <summary>
        /// Tests that an unreachable peer neither throws nor keeps the others from receiving.
        /// </summary>
        [Fact]
        public async Task SurvivesUnreachablePeer()
        {
            var handler = new FailingHandler();
            var transport = new HttpClusterTransport("node-a", Secret, ["http://down/", "http://up/"], handler: handler);

            await transport.SendAsync("topic", [1], TestContext.Current.CancellationToken);

            Assert.Equal(2, handler.Attempts);
        }

        /// <summary>
        /// Tests that a weak secret and a malformed peer are refused at construction, before the
        /// server listens.
        /// </summary>
        [Fact]
        public void RefusesWeakConfiguration()
        {
            Assert.Throws<ArgumentException>(() => new HttpClusterTransport("node", new byte[16], ["http://peer/"]));
            Assert.Throws<ArgumentException>(() => new HttpClusterTransport("node", Secret, ["ftp://peer/"]));
            Assert.Throws<ArgumentException>(() => new HttpClusterTransport("node", Secret, ["dns://service"]));
        }

        /// <summary>
        /// Fails the first peer and counts every attempt.
        /// </summary>
        private sealed class FailingHandler : HttpMessageHandler
        {
            internal int Attempts;

            /// <summary>
            /// Fails requests to the host "down".
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A successful response for every other host.</returns>
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Attempts);

                if (request.RequestUri.Host == "down")
                {
                    throw new HttpRequestException("connection refused");
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
        }
    }
}
