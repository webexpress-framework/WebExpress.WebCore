using System.Text;
using WebExpress.WebCore.WebCluster;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests both cluster stores against the same contract, since subsystems switch between them
    /// only by configuration and must not notice the difference.
    /// </summary>
    public sealed class UnitTestClusterStore : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "wx-cluster-" + Guid.NewGuid().ToString("N"));
        private readonly TestClock _clock = new();

        /// <summary>
        /// Moves time forward on demand, so expiry is tested without waiting.
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
        /// Lists the store kinds every contract test runs against.
        /// </summary>
        public static TheoryData<string> Kinds => ["memory", "file"];

        /// <summary>
        /// Removes the store directory.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// Creates a store of the given kind.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The store.</returns>
        private IClusterStore Create(string kind)
        {
            return kind == "file" ? new FileClusterStore(_directory, _clock) : new MemoryClusterStore(_clock);
        }

        /// <summary>
        /// Tests that a stored value reads back and an absent one reads as null.
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void SetAndGet(string kind)
        {
            var store = Create(kind);

            store.Set("session", "a", Encoding.UTF8.GetBytes("value"), TimeSpan.FromMinutes(1));

            Assert.Equal("value", Encoding.UTF8.GetString(store.Get("session", "a")));
            Assert.Null(store.Get("session", "b"));
            Assert.Null(store.Get("other", "a"));
        }

        /// <summary>
        /// Tests that an expired entry behaves exactly like an absent one.
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void ExpiredEntryIsAbsent(string kind)
        {
            var store = Create(kind);

            store.Set("session", "a", [1], TimeSpan.FromMinutes(1));
            _clock.Now += TimeSpan.FromMinutes(2);

            Assert.Null(store.Get("session", "a"));
            Assert.Empty(store.List("session"));
            Assert.False(store.Remove("session", "a"));
        }

        /// <summary>
        /// Tests that only the first add of a key wins while it is live.
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void TryAddWinsOnce(string kind)
        {
            var store = Create(kind);

            Assert.True(store.TryAdd("job", "run", [1], TimeSpan.FromMinutes(1)));
            Assert.False(store.TryAdd("job", "run", [2], TimeSpan.FromMinutes(1)));
            Assert.Equal([1], store.Get("job", "run"));
        }

        /// <summary>
        /// Tests that an expired entry no longer blocks an add, so a lock whose owner died frees up.
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void TryAddReplacesExpired(string kind)
        {
            var store = Create(kind);

            store.TryAdd("lock", "packages", [1], TimeSpan.FromMinutes(1));
            _clock.Now += TimeSpan.FromMinutes(2);

            Assert.True(store.TryAdd("lock", "packages", [2], TimeSpan.FromMinutes(1)));
            Assert.Equal([2], store.Get("lock", "packages"));
        }

        /// <summary>
        /// Tests that of many concurrent adds of one key exactly one succeeds - the guarantee job
        /// claims rely on. Separate store instances over one directory stand in for instances.
        /// </summary>
        [Fact]
        public void TryAddIsAtomicAcrossInstances()
        {
            var stores = Enumerable.Range(0, 8).Select(_ => new FileClusterStore(_directory)).ToArray();
            var wins = 0;

            Parallel.For(0, 64, i =>
            {
                if (stores[i % stores.Length].TryAdd("job", "app|job|202610041200", [(byte)i], TimeSpan.FromMinutes(5)))
                {
                    Interlocked.Increment(ref wins);
                }
            });

            Assert.Equal(1, wins);
        }

        /// <summary>
        /// Tests that removing returns whether a live entry existed.
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void Remove(string kind)
        {
            var store = Create(kind);

            store.Set("session", "a", [1], TimeSpan.FromMinutes(1));

            Assert.True(store.Remove("session", "a"));
            Assert.False(store.Remove("session", "a"));
            Assert.Null(store.Get("session", "a"));
        }

        /// <summary>
        /// Tests that a listing returns the original keys of the live entries of one scope.
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void ListAndCount(string kind)
        {
            var store = Create(kind);

            store.Set("notification", "x", [1], TimeSpan.FromMinutes(1));
            store.Set("notification", "y", [2], TimeSpan.FromMinutes(1));
            store.Set("session", "z", [3], TimeSpan.FromMinutes(1));

            var list = store.List("notification").OrderBy(x => x.Key).ToList();

            Assert.Equal(["x", "y"], list.Select(x => x.Key));
            Assert.Equal([2], list[1].Value);
            Assert.Equal(2, store.Count("notification"));
            Assert.Equal(0, store.Count("empty"));
        }

        /// <summary>
        /// Tests that the file store reports itself shared and the memory store does not, which
        /// is what every subsystem decides its behavior on.
        /// </summary>
        [Fact]
        public void IsShared()
        {
            Assert.True(new FileClusterStore(_directory).IsShared);
            Assert.False(new MemoryClusterStore().IsShared);
        }

        /// <summary>
        /// Tests that a key cannot leave its scope directory and a scope cannot leave the store.
        /// </summary>
        [Fact]
        public void PathsStayInsideTheStore()
        {
            var store = new FileClusterStore(_directory);

            store.Set("session", "../../escape", [1], TimeSpan.FromMinutes(1));

            Assert.Single(Directory.GetFiles(Path.Combine(_directory, "session"), "*.entry"));
            Assert.Throws<ArgumentException>(() => store.Set("../escape", "a", [1], TimeSpan.FromMinutes(1)));
            Assert.Throws<ArgumentException>(() => store.Get("Upper", "a"));
        }

        /// <summary>
        /// Tests that the sweep deletes expired entries and keeps live ones.
        /// </summary>
        [Fact]
        public void PruneRemovesExpired()
        {
            var store = new FileClusterStore(_directory, _clock);

            store.Set("session", "old", [1], TimeSpan.FromMinutes(1));
            store.Set("session", "new", [2], TimeSpan.FromHours(1));
            _clock.Now += TimeSpan.FromMinutes(2);

            store.PruneExpired();

            Assert.Equal(1, store.Count("session"));
            Assert.NotNull(store.Get("session", "new"));
        }

        /// <summary>
        /// Tests that a foreign file in a scope directory is neither listed nor fatal.
        /// </summary>
        [Fact]
        public void CorruptEntryIsIgnored()
        {
            var store = new FileClusterStore(_directory);

            store.Set("session", "a", [1], TimeSpan.FromMinutes(1));
            File.WriteAllText(Path.Combine(_directory, "session", "garbage.entry"), "not an entry");

            Assert.Single(store.List("session"));
        }
    }
}
