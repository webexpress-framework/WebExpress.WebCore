using System;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Isolates durable replay protection from stateless access-token verification.
    /// Implementations must coordinate atomic consumption across every server instance.
    /// </summary>
    public interface IIdentityTokenStore
    {
        /// <summary>
        /// Records a token identifier exactly once so concurrent refreshes cannot both succeed.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <param name="expiresAt">The deadline until which the replay or revocation marker must be retained.</param>
        /// <returns>True when this operation created the first durable marker; otherwise, false.</returns>
        bool TryConsume(string tokenId, DateTimeOffset expiresAt);
        /// <summary>
        /// Retains a revocation at least until the credential can no longer be accepted.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <param name="expiresAt">The deadline until which the replay or revocation marker must be retained.</param>
        void Revoke(string tokenId, DateTimeOffset expiresAt);
        /// <summary>
        /// Fails a revoked credential before any identity is granted to its caller.
        /// </summary>
        /// <param name="tokenId">The unique credential or grant identifier whose durable marker is accessed.</param>
        /// <returns>True when a durable denial marker exists; otherwise, false.</returns>
        bool IsRevoked(string tokenId);
    }
}
