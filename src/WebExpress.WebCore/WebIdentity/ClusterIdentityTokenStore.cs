using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WebExpress.WebCore.WebCluster;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Keeps the replay and revocation markers in the cluster store, so a cluster needs no token
    /// directory of its own for its authentication. The cluster store already provides what the
    /// markers require: entries every instance sees, an atomic add for the first redemption of a
    /// credential, and an expiry that removes a marker once its credential can no longer be used.
    /// </summary>
    /// <remarks>
    /// The store is only used while it is shared, since a marker kept in one process would let
    /// another instance redeem a refresh token a second time. Markers must also survive a restart
    /// of the store, or revoked credentials become usable again: a store supplied by a plugin has to
    /// be durable, not merely a cache.
    /// </remarks>
    public sealed class ClusterIdentityTokenStore : IIdentityTokenStore
    {
        /// <summary>
        /// The scope the markers are kept under in the cluster store.
        /// </summary>
        internal const string StoreScope = "token";

        // a marker for a credential that is already past its deadline still has to be written
        // atomically; it is kept briefly, since the credential is rejected as expired anyway
        private static readonly TimeSpan MinimumLifetime = TimeSpan.FromMinutes(1);

        private readonly IClusterManager _cluster;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="cluster">The cluster manager whose current store holds the markers.</param>
        /// <param name="timeProvider">The clock turning credential deadlines into marker lifetimes.</param>
        public ClusterIdentityTokenStore(IClusterManager cluster, TimeProvider timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(cluster);

            _cluster = cluster;
            _clock = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Records a credential exactly once across every instance.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <param name="expiresAt">The deadline until which the replay or revocation marker must be retained.</param>
        /// <returns>True when this operation created the first durable marker; otherwise, false.</returns>
        public bool TryConsume(string tokenId, DateTimeOffset expiresAt)
        {
            return Store.TryAdd(StoreScope, Key(tokenId), Value(expiresAt), Lifetime(expiresAt));
        }

        /// <summary>
        /// Retains a revocation at least until the credential can no longer be accepted.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <param name="expiresAt">The deadline until which the replay or revocation marker must be retained.</param>
        public void Revoke(string tokenId, DateTimeOffset expiresAt)
        {
            // an existing marker is kept rather than replaced: it may already carry a later deadline
            Store.TryAdd(StoreScope, Key(tokenId), Value(expiresAt), Lifetime(expiresAt));
        }

        /// <summary>
        /// Determines whether a denial marker exists for a credential.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <returns>True when a durable denial marker exists; otherwise, false.</returns>
        public bool IsRevoked(string tokenId)
        {
            return Store.Get(StoreScope, Key(tokenId)) is not null;
        }

        /// <summary>
        /// Returns the current cluster store, refusing one that only this process sees.
        /// </summary>
        private IClusterStore Store => _cluster.Store is { IsShared: true } store
            ? store
            : throw new InvalidOperationException("The cluster store is not shared and cannot hold replay or revocation markers.");

        /// <summary>
        /// Hashes a credential identifier, so the store never holds raw identifiers.
        /// </summary>
        /// <param name="tokenId">The credential identifier.</param>
        /// <returns>The key of its marker.</returns>
        private static string Key(string tokenId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenId)));
        }

        /// <summary>
        /// Encodes the deadline of a marker, for inspection of the store.
        /// </summary>
        /// <param name="expiresAt">The deadline.</param>
        /// <returns>The value of the marker.</returns>
        private static byte[] Value(DateTimeOffset expiresAt)
        {
            return Encoding.ASCII.GetBytes(expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Turns a credential deadline into the lifetime of its marker.
        /// </summary>
        /// <param name="expiresAt">The deadline.</param>
        /// <returns>The lifetime.</returns>
        private TimeSpan Lifetime(DateTimeOffset expiresAt)
        {
            var lifetime = expiresAt - _clock.GetUtcNow();

            return lifetime < MinimumLifetime ? MinimumLifetime : lifetime;
        }
    }
}
