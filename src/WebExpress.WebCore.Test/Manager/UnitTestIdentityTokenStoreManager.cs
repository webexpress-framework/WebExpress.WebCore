using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Verifies that replay and revocation markers always reach exactly one store per application,
    /// and that the credential logic in the identity manager follows the store bound at call time.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestIdentityTokenStoreManager
    {
        /// <summary>
        /// Without a plugin store, every application shares the file store configured by the deployment.
        /// </summary>
        [Fact]
        public void DefaultStoreIsTheSharedFileStore()
        {
            using var fixture = new AuthenticationFixture();
            var other = fixture.Hub.ApplicationManager.GetApplications(typeof(TestApplicationB)).First();
            var store = fixture.Hub.IdentityTokenStoreManager.GetStore(fixture.Application);
            Assert.IsType<FileIdentityTokenStore>(store);
            Assert.Same(store, fixture.Hub.IdentityTokenStoreManager.GetStore(other));
            Assert.Equal(new[] { store }, fixture.Hub.IdentityTokenStoreManager.Stores);
            Assert.Null(fixture.Hub.IdentityTokenStoreManager.GetStore(null));
        }

        /// <summary>
        /// A registered store receives the application's refresh markers, replaces an earlier binding instead of
        /// joining it, and leaves other applications on the default store until it is unregistered.
        /// </summary>
        [Fact]
        public void RegisteredStoreReplacesTheDefaultForItsApplicationOnly()
        {
            using var fixture = new AuthenticationFixture();
            var manager = fixture.Hub.IdentityTokenStoreManager;
            var other = fixture.Hub.ApplicationManager.GetApplications(typeof(TestApplicationB)).First();
            var first = new MemoryTokenStore();
            var second = new MemoryTokenStore();
            manager.Register(first, fixture.Application);
            manager.Register(second, fixture.Application);
            Assert.Same(second, manager.GetStore(fixture.Application));
            Assert.DoesNotContain(first, manager.Stores);
            Assert.False(first.Disposed, "an explicitly registered store stays owned by its caller");
            Assert.IsType<FileIdentityTokenStore>(manager.GetStore(other));

            var pair = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), fixture.Application);
            Assert.NotNull(fixture.Manager.Refresh(pair.RefreshToken, fixture.Application));
            Assert.Contains(second.Markers, x => x.StartsWith("refresh:"));
            Assert.Null(fixture.Manager.Refresh(pair.RefreshToken, fixture.Application));
            Assert.Contains(second.Markers, x => x.StartsWith("grant:"));
            Assert.Empty(first.Markers);

            Assert.False(manager.Unregister(first, fixture.Application));
            Assert.True(manager.Unregister(second, fixture.Application));
            Assert.IsType<FileIdentityTokenStore>(manager.GetStore(fixture.Application));
        }

        /// <summary>
        /// Without durable storage, stateless access still works, but every operation that depends on a replay
        /// or revocation marker fails instead of accepting a credential that might have been revoked.
        /// </summary>
        [Fact]
        public void MissingStoreFailsMarkerDependentOperations()
        {
            using var fixture = new AuthenticationFixture(withTokenStorePath: false);
            var app = fixture.Application;
            Assert.Null(fixture.Hub.IdentityTokenStoreManager.GetStore(app));
            Assert.False(fixture.Manager.IsAuthenticationConfigured(app));
            var owner = new Identity(Guid.NewGuid(), "alice", permissions: ["read"]);
            var pair = fixture.Manager.Issue(owner, app);
            Assert.NotNull(fixture.Manager.ValidateAccessToken(pair.AccessToken, app));
            Assert.Null(fixture.Manager.Refresh(pair.RefreshToken, app));
            var token = fixture.Manager.CreatePersonalAccessToken(owner, app, TimeSpan.FromHours(1), ["read"]);
            Assert.Null(fixture.Manager.ValidatePersonalAccessToken(token, app));
            Assert.Throws<InvalidOperationException>(() => fixture.Manager.RevokePersonalAccessToken(token, app));

            fixture.Hub.IdentityTokenStoreManager.Register(new MemoryTokenStore(), app);
            Assert.True(fixture.Manager.IsAuthenticationConfigured(app));
            Assert.NotNull(fixture.Manager.ValidatePersonalAccessToken(token, app));
        }

        /// <summary>
        /// Removing the owning plugin unbinds and disposes the store, so no request keeps writing into it.
        /// </summary>
        [Fact]
        public void PluginRemovalReleasesTheBoundStore()
        {
            using var fixture = new AuthenticationFixture();
            var store = new MemoryTokenStore();
            fixture.Hub.IdentityTokenStoreManager.Register(store, fixture.Application);
            ((WebPlugin.PluginManager)fixture.Hub.PluginManager).Remove(fixture.Application.PluginContext);
            Assert.True(store.Disposed);
            Assert.DoesNotContain(store, fixture.Hub.IdentityTokenStoreManager.Stores);
            Assert.NotSame(store, fixture.Hub.IdentityTokenStoreManager.GetStore(fixture.Application));
        }

        /// <summary>
        /// Stands in for a plugin-supplied store; kept private so plugin discovery does not bind it to the test applications.
        /// </summary>
        private sealed class MemoryTokenStore : IIdentityTokenStore, IDisposable
        {
            private readonly HashSet<string> _markers = [];
            internal IReadOnlyCollection<string> Markers { get { lock (_markers) { return _markers.ToArray(); } } }
            internal bool Disposed { get; private set; }

            /// <summary>
            /// Mirrors the atomic first-writer semantics required of every store.
            /// </summary>
            /// <param name="tokenId">The unique credential or grant identifier whose marker is accessed.</param>
            /// <param name="expiresAt">The deadline until which the marker must be retained.</param>
            /// <returns>True when this call created the marker; otherwise, false.</returns>
            public bool TryConsume(string tokenId, DateTimeOffset expiresAt)
            {
                lock (_markers) { return _markers.Add(tokenId); }
            }

            /// <summary>
            /// Records a denial marker.
            /// </summary>
            /// <param name="tokenId">The unique credential or grant identifier whose marker is accessed.</param>
            /// <param name="expiresAt">The deadline until which the marker must be retained.</param>
            public void Revoke(string tokenId, DateTimeOffset expiresAt) => TryConsume(tokenId, expiresAt);

            /// <summary>
            /// Reports whether a denial marker exists.
            /// </summary>
            /// <param name="tokenId">The unique credential or grant identifier whose marker is accessed.</param>
            /// <returns>True when a marker exists; otherwise, false.</returns>
            public bool IsRevoked(string tokenId)
            {
                lock (_markers) { return _markers.Contains(tokenId); }
            }

            /// <summary>
            /// Records that the manager released the store.
            /// </summary>
            public void Dispose() => Disposed = true;
        }
    }
}
