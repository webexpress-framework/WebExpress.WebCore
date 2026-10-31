using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebCluster;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests the token store that keeps replay and revocation markers in the cluster store, so a
    /// cluster authenticates without a token directory of its own.
    /// </summary>
    [Collection("NonParallelTests")]
    public sealed class UnitTestClusterIdentityTokenStore : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "wx-cluster-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Removes the state directory.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// Creates a cluster manager of one instance over the shared state directory.
        /// </summary>
        /// <returns>The cluster manager.</returns>
        private IClusterManager CreateCluster()
        {
            var cluster = UnitTestFixture.CreateAndRegisterComponentHubMock().ClusterManager;

            cluster.UseStore(new FileClusterStore(_directory));

            return cluster;
        }

        /// <summary>
        /// Tests that a refresh token is redeemed once across instances, the guarantee that makes a
        /// stolen refresh token detectable.
        /// </summary>
        [Fact]
        public void ConsumeOnceAcrossInstances()
        {
            var stores = Enumerable.Range(0, 4).Select(_ => new ClusterIdentityTokenStore(CreateCluster())).ToArray();
            var expires = DateTimeOffset.UtcNow.AddHours(1);
            var wins = 0;

            Parallel.For(0, 32, i =>
            {
                if (stores[i % stores.Length].TryConsume("refresh:abc", expires))
                {
                    Interlocked.Increment(ref wins);
                }
            });

            Assert.Equal(1, wins);
            Assert.True(stores[3].IsRevoked("refresh:abc"));
        }

        /// <summary>
        /// Tests that a revocation on one instance denies the credential on another, and that the
        /// store holds a hash instead of the credential identifier.
        /// </summary>
        [Fact]
        public void RevocationIsClusterWide()
        {
            var a = CreateCluster();
            var b = CreateCluster();
            var expires = DateTimeOffset.UtcNow.AddDays(1);

            Assert.False(new ClusterIdentityTokenStore(b).IsRevoked("pat:secret-id"));
            new ClusterIdentityTokenStore(a).Revoke("pat:secret-id", expires);
            new ClusterIdentityTokenStore(a).Revoke("pat:secret-id", expires);

            Assert.True(new ClusterIdentityTokenStore(b).IsRevoked("pat:secret-id"));
            var keys = a.Store.List(ClusterIdentityTokenStore.StoreScope).Select(x => x.Key).ToList();
            Assert.Single(keys);
            Assert.DoesNotContain("secret-id", keys[0]);
        }

        /// <summary>
        /// Tests that a credential already past its deadline still gets its marker, since a replay
        /// must be recorded even when the clock of the instance runs slightly behind.
        /// </summary>
        [Fact]
        public void ExpiredDeadlineStillRecords()
        {
            var store = new ClusterIdentityTokenStore(CreateCluster());

            Assert.True(store.TryConsume("refresh:late", DateTimeOffset.UtcNow.AddMinutes(-5)));
            Assert.False(store.TryConsume("refresh:late", DateTimeOffset.UtcNow.AddMinutes(-5)));
        }

        /// <summary>
        /// Tests that a store only this process sees is refused, since it would let another instance
        /// redeem the same refresh token again.
        /// </summary>
        [Fact]
        public void RefusesProcessLocalStore()
        {
            var store = new ClusterIdentityTokenStore(UnitTestFixture.CreateAndRegisterComponentHubMock().ClusterManager);

            Assert.Throws<InvalidOperationException>(() => store.TryConsume("refresh:x", DateTimeOffset.UtcNow.AddHours(1)));
        }

        /// <summary>
        /// Tests that without a token directory the authentication of a cluster keeps its markers in
        /// the shared cluster store, and that a replayed refresh token is detected.
        /// </summary>
        [Fact]
        public void AuthenticationUsesClusterStoreWithoutTokenDirectory()
        {
            using var fixture = new AuthenticationFixture(withTokenStorePath: false);
            ((ClusterManager)fixture.Hub.ClusterManager).Configure(new ClusterSettings { StatePath = _directory });
            var app = fixture.Application;

            Assert.IsType<ClusterIdentityTokenStore>(fixture.Hub.IdentityTokenStoreManager.GetStore(app));
            Assert.True(fixture.Manager.IsAuthenticationConfigured(app));

            var pair = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), app);
            var next = fixture.Manager.Refresh(pair.RefreshToken, app);

            Assert.NotNull(next);
            Assert.Null(fixture.Manager.Refresh(pair.RefreshToken, app));
            Assert.Null(fixture.Manager.Refresh(next.RefreshToken, app));
        }

        /// <summary>
        /// Tests that a configured token directory keeps precedence over the cluster store, so
        /// existing deployments keep their markers where they are.
        /// </summary>
        [Fact]
        public void TokenDirectoryKeepsPrecedence()
        {
            using var fixture = new AuthenticationFixture();
            fixture.Hub.ClusterManager.UseStore(new FileClusterStore(_directory));

            Assert.IsType<FileIdentityTokenStore>(fixture.Hub.IdentityTokenStoreManager.GetStore(fixture.Application));
        }
    }
}
