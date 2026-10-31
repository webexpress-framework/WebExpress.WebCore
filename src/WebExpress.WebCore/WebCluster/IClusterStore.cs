using System;
using System.Collections.Generic;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// Holds the state every instance of a cluster must agree on - sessions, job claims, login
    /// lockouts - outside of any single process, so a request may land on any instance and a
    /// restarted instance loses nothing. Entries are grouped in scopes and expire on their own,
    /// because every user of the store holds state with a natural lifetime and an entry that
    /// outlived its owner must never pile up.
    /// </summary>
    /// <remarks>
    /// Implementations must make <see cref="TryAdd"/> atomic across every instance sharing the
    /// store; it is what lets exactly one instance claim a job run. All other operations may be
    /// last-writer-wins. An expired entry must behave exactly like an absent one.
    /// </remarks>
    public interface IClusterStore
    {
        /// <summary>
        /// Determines whether instances other than this one see the entries. A store that keeps
        /// them in the process makes every user fall back to its single-instance behavior.
        /// </summary>
        bool IsShared { get; }

        /// <summary>
        /// Returns the value of an entry.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <returns>The value, or null when the entry is absent or expired.</returns>
        byte[] Get(string scope, string key);

        /// <summary>
        /// Creates or replaces an entry.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <param name="value">The value.</param>
        /// <param name="lifetime">How long the entry lives before it expires.</param>
        void Set(string scope, string key, byte[] value, TimeSpan lifetime);

        /// <summary>
        /// Creates an entry only when none exists, atomically across every instance.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <param name="value">The value.</param>
        /// <param name="lifetime">How long the entry lives before it expires.</param>
        /// <returns>True when this call created the entry; false when a live entry already existed.</returns>
        bool TryAdd(string scope, string key, byte[] value, TimeSpan lifetime);

        /// <summary>
        /// Removes an entry.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <returns>True when a live entry was removed.</returns>
        bool Remove(string scope, string key);

        /// <summary>
        /// Returns every live entry of a scope. Meant for small scopes - global notifications, the
        /// history of one chat channel - since every entry is read.
        /// </summary>
        /// <param name="scope">The group to list.</param>
        /// <returns>The keys and values of the live entries.</returns>
        IEnumerable<KeyValuePair<string, byte[]>> List(string scope);

        /// <summary>
        /// Counts the entries of a scope without reading them. Entries that expired but have not
        /// been swept yet may be included.
        /// </summary>
        /// <param name="scope">The group to count.</param>
        /// <returns>The number of entries.</returns>
        int Count(string scope);
    }
}
