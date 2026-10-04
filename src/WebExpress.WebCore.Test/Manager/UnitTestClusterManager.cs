using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebCluster;
using WebExpress.WebCore.WebJob;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebSession;
using WebExpress.WebCore.WebSession.Model;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests the cluster manager and the subsystems that change behavior once the server is one
    /// of several instances: sessions and job runs.
    /// </summary>
    [Collection("NonParallelTests")]
    public sealed class UnitTestClusterManager : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "wx-cluster-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// A transport that hands published messages to the test and lets it inject received ones.
        /// </summary>
        private sealed class TestTransport : IClusterTransport
        {
            internal readonly List<(string Topic, byte[] Payload)> Sent = [];

            /// <summary>
            /// Raised for injected messages.
            /// </summary>
            public event EventHandler<ClusterMessage> Received;

            /// <summary>
            /// Records the message.
            /// </summary>
            /// <param name="topic">The topic.</param>
            /// <param name="payload">The content.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A completed task.</returns>
            public Task SendAsync(string topic, byte[] payload, CancellationToken cancellationToken = default)
            {
                Sent.Add((topic, payload));

                return Task.CompletedTask;
            }

            /// <summary>
            /// Simulates a message from another instance.
            /// </summary>
            /// <param name="message">The message.</param>
            internal void Inject(ClusterMessage message) => Received?.Invoke(this, message);

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Removes the shared state directory.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// Creates a hub whose cluster manager shares the test directory, standing in for one instance.
        /// </summary>
        /// <param name="nodeId">The node id of the instance.</param>
        /// <returns>The hub.</returns>
        private WebComponent.ComponentHub CreateInstance(string nodeId)
        {
            var hub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            ((ClusterManager)hub.ClusterManager).Configure(new ClusterSettings { NodeId = nodeId, StatePath = _directory });

            return hub;
        }

        /// <summary>
        /// Creates a request carrying a session cookie.
        /// </summary>
        /// <param name="sessionId">The session id, or null for a request without session.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(Guid? sessionId)
        {
            return UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={sessionId?.ToString() ?? ""}\n\n");
        }

        /// <summary>
        /// Tests that without settings the server keeps its single-instance behavior.
        /// </summary>
        [Fact]
        public void SingleInstanceByDefault()
        {
            var hub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            Assert.NotNull(hub.ClusterManager);
            Assert.False(hub.ClusterManager.IsClustered);
            Assert.False(hub.ClusterManager.Store.IsShared);
            Assert.Null(hub.ClusterManager.Transport);
            Assert.False(string.IsNullOrWhiteSpace(hub.ClusterManager.NodeId));
        }

        /// <summary>
        /// Tests that a state path turns the instance into a cluster member with a shared store.
        /// </summary>
        [Fact]
        public void StatePathMakesClustered()
        {
            var hub = CreateInstance("node-a");

            Assert.True(hub.ClusterManager.IsClustered);
            Assert.True(hub.ClusterManager.Store.IsShared);
            Assert.Equal("node-a", hub.ClusterManager.NodeId);
        }

        /// <summary>
        /// Tests that the cluster block is read from the configuration, the way environment
        /// variables of a container deliver it.
        /// </summary>
        [Fact]
        public void SettingsAreReadFromConfiguration()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["WebExpress:Cluster:NodeId"] = "pod-7",
                ["WebExpress:Cluster:StatePath"] = _directory,
                ["WebExpress:Cluster:Peers:0"] = "dns://webexpress-peers:8080",
                ["WebExpress:Cluster:Secret"] = Convert.ToBase64String(new byte[32])
            }).Build();

            var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(UnitTestFixture.CreateHttpServerContextMock(configuration: configuration));

            Assert.Equal("pod-7", hub.ClusterManager.NodeId);
            Assert.True(hub.ClusterManager.Store.IsShared);
            Assert.IsType<HttpClusterTransport>(hub.ClusterManager.Transport);
        }

        /// <summary>
        /// Tests that peers without a strong secret stop the start instead of opening an
        /// unauthenticated endpoint.
        /// </summary>
        [Fact]
        public void PeersRequireSecret()
        {
            var cluster = (ClusterManager)UnitTestFixture.CreateAndRegisterComponentHubMock().ClusterManager;

            Assert.Throws<InvalidOperationException>(() => cluster.Configure(new ClusterSettings { Peers = ["http://peer/"] }));
            Assert.Throws<InvalidOperationException>(() => cluster.Configure(new ClusterSettings { Peers = ["http://peer/"], Secret = Convert.ToBase64String(new byte[16]) }));

            cluster.Configure(new ClusterSettings { Peers = ["http://peer/"], Secret = Convert.ToBase64String(new byte[32]) });

            Assert.IsType<HttpClusterTransport>(cluster.Transport);
            Assert.True(cluster.IsClustered);
        }

        /// <summary>
        /// Tests that the node id falls back to the host name container runtimes set.
        /// </summary>
        [Fact]
        public void NodeIdFallsBackToHostName()
        {
            Assert.Equal("configured", ClusterManager.ResolveNodeId(" configured ", _ => "pod-1"));
            Assert.Equal("pod-1", ClusterManager.ResolveNodeId(null, x => x == "HOSTNAME" ? "pod-1" : null));
            Assert.Equal(Environment.MachineName, ClusterManager.ResolveNodeId(null, _ => null));
        }

        /// <summary>
        /// Tests that a lock is exclusive across instances and free again once released.
        /// </summary>
        [Fact]
        public void LockIsExclusive()
        {
            var a = CreateInstance("node-a").ClusterManager;
            var b = CreateInstance("node-b").ClusterManager;

            var held = a.Lock("packages", TimeSpan.FromMinutes(1), TimeSpan.Zero);

            Assert.NotNull(held);
            Assert.Null(b.Lock("packages", TimeSpan.FromMinutes(1), TimeSpan.Zero));

            held.Dispose();

            using var taken = b.Lock("packages", TimeSpan.FromMinutes(1), TimeSpan.Zero);
            Assert.NotNull(taken);
        }

        /// <summary>
        /// Tests that received messages reach the subscribers of their topic only, and that a
        /// disposed subscription stops receiving.
        /// </summary>
        [Fact]
        public async Task PublishAndSubscribe()
        {
            var cluster = UnitTestFixture.CreateAndRegisterComponentHubMock().ClusterManager;
            var transport = new TestTransport();
            var received = new List<string>();
            cluster.UseTransport(transport);

            var subscription = cluster.Subscribe("topic", x => received.Add(x.Origin));
            cluster.Subscribe("other", _ => received.Add("wrong"));

            await cluster.PublishAsync("topic", [1], TestContext.Current.CancellationToken);
            transport.Inject(new ClusterMessage("topic", "node-b", [1]));
            subscription.Dispose();
            transport.Inject(new ClusterMessage("topic", "node-c", [1]));

            Assert.Equal(["node-b"], received);
            Assert.Equal("topic", Assert.Single(transport.Sent).Topic);
        }

        /// <summary>
        /// Tests that the clock difference to each sending instance is measured from the time its
        /// messages carry, ahead positive and behind negative.
        /// </summary>
        [Fact]
        public void ClockSkewIsMeasured()
        {
            var cluster = UnitTestFixture.CreateAndRegisterComponentHubMock().ClusterManager;
            var transport = new TestTransport();
            cluster.UseTransport(transport);

            transport.Inject(new ClusterMessage("topic", "ahead", [], DateTimeOffset.UtcNow.AddSeconds(30)));
            transport.Inject(new ClusterMessage("topic", "behind", [], DateTimeOffset.UtcNow.AddSeconds(-12)));
            transport.Inject(new ClusterMessage("topic", "untimed", []));

            Assert.InRange(cluster.ClockSkew["ahead"].TotalSeconds, 29, 31);
            Assert.InRange(cluster.ClockSkew["behind"].TotalSeconds, -13, -11);
            Assert.False(cluster.ClockSkew.ContainsKey("untimed"));
        }

        /// <summary>
        /// Tests that a session created on one instance, with its properties, is found by another
        /// instance - the round-robin case every load balancer produces.
        /// </summary>
        [Fact]
        public void SessionMovesBetweenInstances()
        {
            var a = CreateInstance("node-a");
            var session = a.SessionManager.GetSession(CreateRequest(null));
            session.SetProperty(new SessionPropertyParameter(new Parameter("filter", "open", ParameterScope.Session)));
            a.SessionManager.Commit(session);

            var b = CreateInstance("node-b");
            var resumed = b.SessionManager.GetSession(CreateRequest(session.Id));

            Assert.Equal(session.Id, resumed.Id);
            Assert.Equal("open", resumed.GetProperty<SessionPropertyParameter>()?.Params["filter"].Value);
            Assert.Equal(1, b.SessionManager.Count);
        }

        /// <summary>
        /// Tests that an id the cluster never issued yields a fresh session with a new id, so a
        /// planted cookie cannot fix the id of a session.
        /// </summary>
        [Fact]
        public void SessionUnknownIdIsNotAdopted()
        {
            var hub = CreateInstance("node-a");
            var planted = Guid.NewGuid();

            var session = hub.SessionManager.GetSession(CreateRequest(planted));

            Assert.NotEqual(planted, session.Id);
        }

        /// <summary>
        /// Tests that the old id stops resolving on every instance once a sign-in replaced it.
        /// </summary>
        [Fact]
        public void SessionRegenerateIdIsClusterWide()
        {
            var a = CreateInstance("node-a");
            var session = a.SessionManager.GetSession(CreateRequest(null));
            a.SessionManager.Commit(session);
            var old = session.Id;

            var renewed = a.SessionManager.RegenerateId(session);

            var b = CreateInstance("node-b");
            Assert.NotEqual(old, b.SessionManager.GetSession(CreateRequest(old)).Id);
            Assert.Equal(renewed, b.SessionManager.GetSession(CreateRequest(renewed)).Id);
        }

        /// <summary>
        /// Tests that a request that only read its session does not rewrite it, so page views do
        /// not turn into writes to shared storage.
        /// </summary>
        [Fact]
        public void SessionUnchangedIsNotRewritten()
        {
            var hub = CreateInstance("node-a");
            var session = hub.SessionManager.GetSession(CreateRequest(null));
            hub.SessionManager.Commit(session);

            var file = Directory.GetFiles(Path.Combine(_directory, SessionManager.StoreScope)).Single();
            var written = File.GetLastWriteTimeUtc(file);
            Thread.Sleep(20);

            var again = CreateInstance("node-b").SessionManager;
            again.Commit(again.GetSession(CreateRequest(session.Id)));

            Assert.Equal(written, File.GetLastWriteTimeUtc(file));
        }

        /// <summary>
        /// Tests that a property type unknown to the reading instance is dropped without losing
        /// the session, and that no stored type name can make it recreate a type that is not a
        /// session property.
        /// </summary>
        [Fact]
        public void SessionDropsUnknownPropertyTypes()
        {
            var id = Guid.NewGuid();
            var json = """
                {"id":"ID","created":"2026-10-04T10:00:00","updated":"2026-10-04T10:00:00","properties":{
                  "System.IO.FileInfo, System.Runtime":{"fileName":"x"},
                  "Missing.Type, Missing":{},
                  "WebExpress.WebCore.WebSession.Model.SessionPropertyParameter, WebExpress.WebCore":{"Params":{}}
                }}
                """.Replace("ID", id.ToString());
            var dropped = new List<string>();

            var session = SessionSerializer.Deserialize(System.Text.Encoding.UTF8.GetBytes(json), dropped.Add)?.Session;

            Assert.Equal(id, session.Id);
            Assert.Equal(2, dropped.Count);
            Assert.Equal([typeof(SessionPropertyParameter)], session.Properties.Keys);
        }

        /// <summary>
        /// Tests that two requests of one session, running at the same time on two instances,
        /// keep each other's changes instead of the later one overwriting the earlier.
        /// </summary>
        [Fact]
        public void SessionConcurrentChangesAreMerged()
        {
            // arrange: a session both instances load before either writes
            var a = CreateInstance("node-a");
            var created = a.SessionManager.GetSession(CreateRequest(null));
            created.SetProperty(new SessionPropertyParameter(new Parameter("theme", "dark", ParameterScope.Session)));
            a.SessionManager.Commit(created);

            var onA = a.SessionManager.GetSession(CreateRequest(created.Id));
            var b = CreateInstance("node-b");
            var onB = b.SessionManager.GetSession(CreateRequest(created.Id));

            // act: each instance changes something else, then both write
            onA.GetProperty<SessionPropertyParameter>().Params["filter"] = new Parameter("filter", "open", ParameterScope.Session);
            onB.GetProperty<SessionPropertyParameter>().Params["page"] = new Parameter("page", "3", ParameterScope.Session);
            onB.GetProperty<SessionPropertyParameter>().Params.Remove("theme");
            a.SessionManager.Commit(onA);
            b.SessionManager.Commit(onB);

            // validation
            var merged = CreateInstance("node-c").SessionManager.GetSession(CreateRequest(created.Id)).GetProperty<SessionPropertyParameter>().Params;
            Assert.Equal("open", merged["filter"].Value);
            Assert.Equal("3", merged["page"].Value);
            Assert.False(merged.ContainsKey("theme"));
        }

        /// <summary>
        /// Tests the merge rules: one-sided changes win, sets of elements combine, and on a real
        /// conflict the side being written wins.
        /// </summary>
        [Fact]
        public void SessionMergeRules()
        {
            static System.Text.Json.Nodes.JsonNode Parse(string json) => System.Text.Json.Nodes.JsonNode.Parse(json);

            var original = Parse("""{"a":1,"b":{"x":1},"list":[1,2],"gone":1}""");
            var ours = Parse("""{"a":2,"b":{"x":1},"list":[1,2,3],"gone":1}""");
            var theirs = Parse("""{"a":1,"b":{"x":1,"y":2},"list":[2,4]}""");

            var merged = SessionMerge.Merge(original, ours, theirs);

            Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(Parse("""{"a":2,"b":{"x":1,"y":2},"list":[2,4,3]}"""), merged), merged.ToJsonString());
            Assert.Equal(5, SessionMerge.Merge(Parse("1"), Parse("5"), Parse("7")).GetValue<int>());
            Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(theirs, SessionMerge.Merge(original, original, theirs)));
        }

        /// <summary>
        /// Tests that a property the serializer cannot handle neither fails the request nor keeps
        /// the other properties from being shared.
        /// </summary>
        [Fact]
        public void SessionUnserializablePropertyStaysLocal()
        {
            var a = CreateInstance("node-a");
            var session = a.SessionManager.GetSession(CreateRequest(null));
            session.SetProperty(new SessionPropertyParameter(new Parameter("kept", "yes", ParameterScope.Session)));
            session.SetProperty(new CyclicProperty());

            a.SessionManager.Commit(session);

            var resumed = CreateInstance("node-b").SessionManager.GetSession(CreateRequest(session.Id));
            Assert.Equal("yes", resumed.GetProperty<SessionPropertyParameter>().Params["kept"].Value);
            Assert.Null(resumed.GetProperty<CyclicProperty>());
        }

        /// <summary>
        /// A session property referring to itself, which no json serializer can write.
        /// </summary>
        public sealed class CyclicProperty : ISessionProperty
        {
            /// <summary>
            /// Gets the property itself.
            /// </summary>
            public CyclicProperty Self => this;
        }

        /// <summary>
        /// Tests that of two instances evaluating the same due minute only one runs the job, while
        /// a job declared node-local runs on both.
        /// </summary>
        [Fact]
        public void JobRunsOncePerCluster()
        {
            var a = (JobManager)CreateInstance("node-a").JobManager;
            var b = (JobManager)CreateInstance("node-b").JobManager;
            var clock = new Clock(new DateTime(2026, 10, 4, 12, 0, 0));
            var cluster = new JobContext { JobId = new WebComponent.ComponentId("test.job"), Scope = JobScope.Cluster };
            var node = new JobContext { JobId = new WebComponent.ComponentId("test.cache"), Scope = JobScope.Node };

            Assert.True(a.Claim(cluster, clock));
            Assert.False(b.Claim(cluster, clock));
            Assert.True(b.Claim(cluster, new Clock(new DateTime(2026, 10, 4, 12, 1, 0))));
            Assert.True(a.Claim(node, clock));
            Assert.True(b.Claim(node, clock));
        }

        /// <summary>
        /// Tests that the scope attribute is read when a job is registered.
        /// </summary>
        [Fact]
        public void JobScopeAttributeIsRead()
        {
            var hub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var jobs = hub.JobManager.Jobs.Where(x => x.JobId.ToString().EndsWith("testjoba", StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.NotEmpty(jobs);
            Assert.All(jobs, x => Assert.Equal(JobScope.Node, x.Scope));

            // a job without the attribute acts on shared data and runs once per cluster
            Assert.Equal(JobScope.Cluster, new JobContext().Scope);
        }
    }
}
