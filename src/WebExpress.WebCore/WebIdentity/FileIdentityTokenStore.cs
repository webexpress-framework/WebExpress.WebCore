using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Stores only replay and revocation markers; no identity or session is kept on the server.
    /// Multiple instances must share a filesystem that implements atomic exclusive file creation.
    /// </summary>
    public sealed class FileIdentityTokenStore : IIdentityTokenStore
    {
        private readonly string _directory;

        /// <summary>
        /// Requires an explicit durable location to avoid silently losing revocations on restart.
        /// </summary>
        /// <param name="directory">The durable shared directory used for replay and revocation markers.</param>
        public FileIdentityTokenStore(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            _directory = Path.GetFullPath(directory);
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
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.Write(expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                return true;
            }
            catch (IOException) when (File.Exists(path))
            {
                return false;
            }
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
