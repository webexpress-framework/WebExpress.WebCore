using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Stores only replay and revocation markers; no identity or session is kept on the server.
    /// Multiple instances must share a filesystem that implements atomic exclusive file creation.
    /// </summary>
    public sealed class FileIdentityTokenStore : IIdentityTokenStore
    {
        /// <summary>
        /// How often expired markers are removed. Every login, refresh and revocation leaves one
        /// marker behind, so without a sweep the directory only ever grows; an hour keeps it close
        /// to the set of live credentials without scanning it on every write.
        /// </summary>
        internal static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

        private readonly string _directory;
        private readonly TimeProvider _clock;
        private long _nextSweepTicks;

        /// <summary>
        /// Requires an explicit durable location to avoid silently losing revocations on restart.
        /// </summary>
        /// <param name="directory">The durable shared directory used for replay and revocation markers.</param>
        /// <param name="timeProvider">The clock deciding when a marker has expired, replaceable so tests can cross deadlines.</param>
        public FileIdentityTokenStore(string directory, TimeProvider timeProvider = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            _directory = Path.GetFullPath(directory);
            _clock = timeProvider ?? TimeProvider.System;
            // the first sweep waits an interval too: leftovers do no harm until then, and a store that
            // is created and dropped again - a reload, a test - never races a sweep over its directory
            _nextSweepTicks = (_clock.GetUtcNow() + SweepInterval).UtcTicks;
            Directory.CreateDirectory(_directory);
        }

        /// <summary>
        /// Uses exclusive file creation to let only one instance redeem a credential.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <param name="expiresAt">The deadline until which the replay or revocation marker must be retained.</param>
        /// <returns>True when this operation created the first durable marker; otherwise, false.</returns>
        public bool TryConsume(string tokenId, DateTimeOffset expiresAt)
        {
            var path = MarkerPath(tokenId);
            try
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                }

                ScheduleSweep();
                return true;
            }
            catch (IOException) when (File.Exists(path))
            {
                return false;
            }
        }

        /// <summary>
        /// Removes every marker whose retention deadline has passed. A marker outlives the credential
        /// it guards - the deadline is the credential's own expiry - so the credential is rejected
        /// as expired by then and the marker no longer decides anything.
        /// </summary>
        internal void PruneExpired()
        {
            var now = _clock.GetUtcNow().ToUnixTimeSeconds();

            try
            {
                foreach (var path in Directory.EnumerateFiles(_directory, "*.token"))
                {
                    try
                    {
                        if (IsExpired(path, now))
                        {
                            File.Delete(path);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // another instance sharing the directory may be deleting the same marker
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // the directory itself became unreadable; the next sweep tries again
            }
        }

        /// <summary>
        /// Reads the retention deadline of a marker.
        /// </summary>
        /// <param name="path">The marker file.</param>
        /// <param name="now">The current time in unix seconds.</param>
        /// <returns>True when the marker carries a complete deadline that has passed.</returns>
        private static bool IsExpired(string path, long now)
        {
            // shared generously so the sweep never blocks a writer or another instance deleting the marker
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();

            // a marker caught mid-write shows a prefix of its deadline - a smaller number that would make
            // a live marker look expired. current deadlines have ten digits, so anything shorter is
            // left for the next sweep
            return text.Length >= 10
                && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
                && expires < now;
        }

        /// <summary>
        /// Starts a sweep once the interval has passed. It runs in the background because the
        /// directory can hold many markers and the login that crosses the interval should not wait.
        /// </summary>
        private void ScheduleSweep()
        {
            var now = _clock.GetUtcNow().UtcTicks;
            var due = Interlocked.Read(ref _nextSweepTicks);

            // the exchange lets exactly one of several concurrent writers start the sweep
            if (now < due || Interlocked.CompareExchange(ref _nextSweepTicks, now + SweepInterval.Ticks, due) != due)
            {
                return;
            }

            _ = Task.Run(PruneExpired);
        }

        /// <summary>
        /// Retains a durable denial marker after a credential or grant is revoked.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <param name="expiresAt">The deadline until which the replay or revocation marker must be retained.</param>
        public void Revoke(string tokenId, DateTimeOffset expiresAt) => TryConsume(tokenId, expiresAt);

        /// <summary>
        /// Checks durable markers before refresh or personal credentials can be used.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <returns>True when a durable denial marker exists; otherwise, false.</returns>
        public bool IsRevoked(string tokenId)
        {
            try
            {
                File.GetAttributes(MarkerPath(tokenId));
                return true;
            }
            catch (FileNotFoundException) { return false; }
        }

        /// <summary>
        /// Hashes credential identifiers so storage paths cannot reveal raw identifiers or contain caller-controlled paths.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <returns>The path of the hashed replay or revocation marker.</returns>
        private string MarkerPath(string tokenId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
            return Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenId))) + ".token");
        }
    }
}
