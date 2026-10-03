using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Every login, refresh and revocation leaves a marker in the file store. Markers whose
    /// credential has expired decide nothing any more and must not accumulate forever.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestFileIdentityTokenStore : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "webexpress-store-" + Guid.NewGuid().ToString("N"));
        private readonly AuthenticationFixture.TestClock _clock = new();

        /// <summary>
        /// A sweep removes exactly the markers past their deadline: live replay and revocation
        /// markers stay in force, and a marker that cannot be read is left alone.
        /// </summary>
        [Fact]
        public void PruneRemovesOnlyExpiredMarkers()
        {
            var store = new FileIdentityTokenStore(_directory, _clock);
            store.TryConsume("refresh:expired", _clock.Now.AddMinutes(1));
            store.TryConsume("refresh:live", _clock.Now.AddHours(2));
            store.Revoke("grant:revoked", _clock.Now.AddHours(2));
            var unreadable = Path.Combine(_directory, "unreadable.token");
            File.WriteAllText(unreadable, "not a deadline");

            // what a sweep sees of a marker caught mid-write: the first digits of a live deadline
            var partial = Path.Combine(_directory, "partial.token");
            File.WriteAllText(partial, _clock.Now.AddHours(2).ToUnixTimeSeconds().ToString()[..4]);

            _clock.Now = _clock.Now.AddHours(1);
            store.PruneExpired();

            Assert.False(store.IsRevoked("refresh:expired"));
            Assert.True(store.IsRevoked("refresh:live"));
            Assert.True(store.IsRevoked("grant:revoked"));
            Assert.True(File.Exists(unreadable));
            Assert.True(File.Exists(partial));
        }

        /// <summary>
        /// The sweep needs no caller: writing a marker starts it once the interval has passed, and
        /// only once per interval however many markers are written.
        /// </summary>
        [Fact]
        public void WritingMarkersSweepsOncePerInterval()
        {
            var store = new FileIdentityTokenStore(_directory, _clock);

            // a new store waits an interval before its first sweep, so this expired marker remains
            store.TryConsume("challenge:first", _clock.Now.AddSeconds(-1));
            Assert.False(SpinWait.SpinUntil(() => !store.IsRevoked("challenge:first"), TimeSpan.FromMilliseconds(500)));

            // once the interval has passed, the next write sweeps
            _clock.Now += FileIdentityTokenStore.SweepInterval;
            store.TryConsume("challenge:second", _clock.Now.AddHours(1));
            Assert.True(SpinWait.SpinUntil(() => !store.IsRevoked("challenge:first"), TimeSpan.FromSeconds(10)));
            Assert.True(store.IsRevoked("challenge:second"));

            // within the next interval a further write starts no sweep
            store.TryConsume("challenge:third", _clock.Now.AddSeconds(-1));
            Assert.False(SpinWait.SpinUntil(() => !store.IsRevoked("challenge:third"), TimeSpan.FromMilliseconds(500)));
        }

        /// <summary>
        /// Removes the isolated marker directory.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                // a background sweep may still hold a marker; the temp directory is left behind then
            }
        }
    }
}
