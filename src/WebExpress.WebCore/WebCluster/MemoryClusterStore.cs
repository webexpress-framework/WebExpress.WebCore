using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// Keeps the cluster state in the process. It is the store of a single-instance deployment:
    /// every user of the store sees <see cref="IsShared"/> as false and keeps its fast in-process
    /// path, while code written against <see cref="IClusterStore"/> still runs unchanged.
    /// </summary>
    public sealed class MemoryClusterStore : IClusterStore
    {
        private readonly ConcurrentDictionary<(string Scope, string Key), Entry> _entries = new();
        private readonly TimeProvider _clock;

        /// <summary>
        /// Pairs a value with the moment it stops counting.
        /// </summary>
        /// <param name="Value">The stored value.</param>
        /// <param name="Expires">The moment the entry expires.</param>
        private sealed record Entry(byte[] Value, DateTimeOffset Expires);

        /// <summary>
        /// Returns false, since no other process sees the entries.
        /// </summary>
        public bool IsShared => false;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="timeProvider">The clock deciding expiry, replaceable so tests can cross deadlines.</param>
        public MemoryClusterStore(TimeProvider timeProvider = null)
        {
            _clock = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Returns the value of an entry.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <returns>The value, or null when the entry is absent or expired.</returns>
        public byte[] Get(string scope, string key)
        {
            return _entries.TryGetValue((scope, key), out var entry) && IsLive(entry) ? entry.Value : null;
        }

        /// <summary>
        /// Creates or replaces an entry.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <param name="value">The value.</param>
        /// <param name="lifetime">How long the entry lives before it expires.</param>
        public void Set(string scope, string key, byte[] value, TimeSpan lifetime)
        {
            ArgumentNullException.ThrowIfNull(value);

            _entries[(scope, key)] = new Entry(value, _clock.GetUtcNow() + lifetime);
        }

        /// <summary>
        /// Creates an entry only when no live one exists.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <param name="value">The value.</param>
        /// <param name="lifetime">How long the entry lives before it expires.</param>
        /// <returns>True when this call created the entry.</returns>
        public bool TryAdd(string scope, string key, byte[] value, TimeSpan lifetime)
        {
            ArgumentNullException.ThrowIfNull(value);

            var fresh = new Entry(value, _clock.GetUtcNow() + lifetime);

            while (true)
            {
                if (_entries.TryAdd((scope, key), fresh))
                {
                    return true;
                }

                if (!_entries.TryGetValue((scope, key), out var existing))
                {
                    continue;
                }

                if (IsLive(existing))
                {
                    return false;
                }

                // replacing only the expired entry that was read keeps two callers that both saw
                // it expired from both winning
                if (_entries.TryUpdate((scope, key), fresh, existing))
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Removes an entry.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <returns>True when a live entry was removed.</returns>
        public bool Remove(string scope, string key)
        {
            return _entries.TryRemove((scope, key), out var entry) && IsLive(entry);
        }

        /// <summary>
        /// Returns every live entry of a scope and drops the expired ones on the way.
        /// </summary>
        /// <param name="scope">The group to list.</param>
        /// <returns>The keys and values of the live entries.</returns>
        public IEnumerable<KeyValuePair<string, byte[]>> List(string scope)
        {
            var result = new List<KeyValuePair<string, byte[]>>();

            foreach (var item in _entries.Where(x => x.Key.Scope == scope).ToList())
            {
                if (IsLive(item.Value))
                {
                    result.Add(new KeyValuePair<string, byte[]>(item.Key.Key, item.Value.Value));
                }
                else
                {
                    _entries.TryRemove(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Counts the live entries of a scope.
        /// </summary>
        /// <param name="scope">The group to count.</param>
        /// <returns>The number of entries.</returns>
        public int Count(string scope)
        {
            return _entries.Count(x => x.Key.Scope == scope && IsLive(x.Value));
        }

        /// <summary>
        /// Determines whether an entry has not expired yet.
        /// </summary>
        /// <param name="entry">The entry to test.</param>
        /// <returns>True while the entry counts.</returns>
        private bool IsLive(Entry entry)
        {
            return entry.Expires > _clock.GetUtcNow();
        }
    }
}
