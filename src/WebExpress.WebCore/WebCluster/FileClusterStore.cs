using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// Keeps the cluster state in a directory every instance mounts, so a cluster needs no service
    /// beyond the shared volume it often has anyway. The file system has to support atomic
    /// renames and, on unix, hard links for the claims that must not replace an existing file,
    /// which local disks, NFS and the usual ReadWriteMany volumes do.
    /// </summary>
    /// <remarks>
    /// Every entry is one file named after the hash of its key, so a key can never reach outside
    /// its scope directory. A file is always written next to its target first and then renamed
    /// into place: a reader on another instance sees either the old or the new content, never a
    /// half-written one. For a store under heavy load a plugin should supply a dedicated store
    /// (a database, a key-value server) instead - a directory scan per listing does not scale indefinitely.
    /// </remarks>
    public sealed partial class FileClusterStore : IClusterStore
    {
        /// <summary>
        /// How often expired entries are removed. Every entry carries its own deadline and an
        /// expired one is never returned, so the sweep only reclaims space and can be rare.
        /// </summary>
        internal static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

        // identifies the file layout, so a later layout change can recognize and skip old files
        private static readonly byte[] Magic = "WXC1"u8.ToArray();

        // a temporary file this old belongs to a writer that died between write and rename
        private static readonly TimeSpan OrphanAge = TimeSpan.FromHours(1);

        // errno of link() for a taken name; the same value on linux, macos and the bsds
        private const int EEXIST = 17;

        private readonly string _directory;
        private readonly TimeProvider _clock;
        private long _nextSweepTicks;

        /// <summary>
        /// Returns true, since every instance mounting the directory sees the entries.
        /// </summary>
        public bool IsShared => true;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="directory">The directory shared by every instance of the cluster.</param>
        /// <param name="timeProvider">The clock deciding expiry, replaceable so tests can cross deadlines.</param>
        public FileClusterStore(string directory, TimeProvider timeProvider = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);

            _directory = Path.GetFullPath(directory);
            _clock = timeProvider ?? TimeProvider.System;
            _nextSweepTicks = (_clock.GetUtcNow() + SweepInterval).UtcTicks;

            Directory.CreateDirectory(_directory);
        }

        /// <summary>
        /// Returns the value of an entry.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <returns>The value, or null when the entry is absent or expired.</returns>
        public byte[] Get(string scope, string key)
        {
            var entry = Read(EntryPath(scope, key));

            return entry is not null && IsLive(entry.Value.Expires) ? entry.Value.Value : null;
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

            var path = EntryPath(scope, key);
            var temp = WriteTemporary(path, key, value, lifetime);

            try
            {
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        File.Move(temp, path, true);
                        break;
                    }
                    catch (Exception ex) when (attempt < 4 && ex is IOException or UnauthorizedAccessException)
                    {
                        // a concurrent replace of the same entry holds the target for a moment
                        Thread.Sleep(5 << attempt);
                    }
                }
            }
            finally
            {
                TryDelete(temp);
            }

            ScheduleSweep();
        }

        /// <summary>
        /// Creates an entry only when no live one exists. The rename that publishes the entry
        /// refuses to replace an existing file, which is what makes the claim atomic across
        /// instances.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry within its scope.</param>
        /// <param name="value">The value.</param>
        /// <param name="lifetime">How long the entry lives before it expires.</param>
        /// <returns>True when this call created the entry.</returns>
        public bool TryAdd(string scope, string key, byte[] value, TimeSpan lifetime)
        {
            ArgumentNullException.ThrowIfNull(value);

            var path = EntryPath(scope, key);
            var temp = WriteTemporary(path, key, value, lifetime);

            try
            {
                // a handful of rounds covers an expired entry being swept or stolen concurrently
                for (var attempt = 0; attempt < 4; attempt++)
                {
                    if (TryPublish(temp, path))
                    {
                        ScheduleSweep();

                        return true;
                    }

                    if (!TryEvictExpired(path))
                    {
                        return false;
                    }
                }

                return false;
            }
            finally
            {
                TryDelete(temp);
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
            var path = EntryPath(scope, key);
            var entry = Read(path);

            if (entry is null)
            {
                return false;
            }

            TryDelete(path);

            return IsLive(entry.Value.Expires);
        }

        /// <summary>
        /// Returns every live entry of a scope.
        /// </summary>
        /// <param name="scope">The group to list.</param>
        /// <returns>The keys and values of the live entries.</returns>
        public IEnumerable<KeyValuePair<string, byte[]>> List(string scope)
        {
            var result = new List<KeyValuePair<string, byte[]>>();
            var directory = ScopeDirectory(scope);

            if (!Directory.Exists(directory))
            {
                return result;
            }

            foreach (var path in EnumerateEntries(directory))
            {
                var entry = Read(path);

                if (entry is not null && IsLive(entry.Value.Expires))
                {
                    result.Add(new KeyValuePair<string, byte[]>(entry.Value.Key, entry.Value.Value));
                }
            }

            return result;
        }

        /// <summary>
        /// Counts the entry files of a scope without opening them.
        /// </summary>
        /// <param name="scope">The group to count.</param>
        /// <returns>The number of entries, expired but unswept ones included.</returns>
        public int Count(string scope)
        {
            var directory = ScopeDirectory(scope);
            var count = 0;

            if (Directory.Exists(directory))
            {
                foreach (var _ in EnumerateEntries(directory))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Removes expired entries and the leftovers of writers that died midway in every scope.
        /// </summary>
        internal void PruneExpired()
        {
            var now = _clock.GetUtcNow();

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(_directory))
                {
                    foreach (var path in Directory.EnumerateFiles(directory))
                    {
                        try
                        {
                            if (path.EndsWith(".entry", StringComparison.Ordinal))
                            {
                                var entry = Read(path);

                                if (entry is not null && !IsLive(entry.Value.Expires))
                                {
                                    File.Delete(path);
                                }
                            }
                            else if (now - File.GetLastWriteTimeUtc(path) > OrphanAge)
                            {
                                File.Delete(path);
                            }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            // another instance sweeping the same directory got there first
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // the directory became unreadable; the next sweep tries again
            }
        }

        /// <summary>
        /// Moves an expired entry out of the way so a new one can be published. The entry is
        /// renamed rather than deleted: of several instances racing for it only one rename
        /// succeeds, and the winner can verify it really took the expired entry - not a fresh
        /// one another instance published in between - before discarding it.
        /// </summary>
        /// <param name="path">The entry that blocked the publication.</param>
        /// <returns>True when the caller should retry the publication.</returns>
        private bool TryEvictExpired(string path)
        {
            var entry = Read(path);

            if (entry is null)
            {
                // vanished or unreadable in between; another round decides
                return true;
            }

            if (IsLive(entry.Value.Expires))
            {
                return false;
            }

            var stale = path + "." + Guid.NewGuid().ToString("N") + ".stale";

            try
            {
                File.Move(path, stale, false);
            }
            catch (IOException)
            {
                // someone else moved it first; the next round sees what is there now
                return true;
            }

            var taken = Read(stale);

            if (taken is not null && IsLive(taken.Value.Expires))
            {
                // the rename caught a fresh entry published after the check; hand it back
                try
                {
                    if (!TryPublish(stale, path))
                    {
                        TryDelete(stale);
                    }
                }
                catch (IOException)
                {
                    TryDelete(stale);
                }

                return false;
            }

            TryDelete(stale);

            return true;
        }

        /// <summary>
        /// Moves a finished file to its final name unless that name is taken, as one atomic step
        /// - the primitive every claim of the store rests on.
        /// </summary>
        /// <remarks>
        /// On unix .NET implements a move without replace as an existence check followed by a
        /// rename, and the rename silently replaces a file another instance published in between.
        /// A hard link fails atomically on an existing name instead, the classic exclusive create
        /// that also holds on NFS. Windows refuses the replace within the move itself.
        /// </remarks>
        /// <param name="source">The finished file.</param>
        /// <param name="target">The name to publish it under.</param>
        /// <returns>True when the file was published; false when the name is taken.</returns>
        private static bool TryPublish(string source, string target)
        {
            if (!OperatingSystem.IsWindows())
            {
                if (Link(source, target) == 0)
                {
                    TryDelete(source);

                    return true;
                }

                if (Marshal.GetLastPInvokeError() == EEXIST)
                {
                    return false;
                }

                // a volume without hard links (some smb mounts) only offers the racy move below
            }

            try
            {
                File.Move(source, target, false);

                return true;
            }
            catch (IOException) when (File.Exists(target))
            {
                return false;
            }
        }

        /// <summary>
        /// Writes an entry into a temporary file next to its target, ready to be renamed into place.
        /// </summary>
        /// <param name="path">The final path of the entry.</param>
        /// <param name="key">The key stored inside the entry, so listings can return it.</param>
        /// <param name="value">The value.</param>
        /// <param name="lifetime">How long the entry lives.</param>
        /// <returns>The path of the temporary file.</returns>
        private string WriteTemporary(string path, string key, byte[] value, TimeSpan lifetime)
        {
            var directory = Path.GetDirectoryName(path);
            var temp = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
            var expires = (_clock.GetUtcNow() + lifetime).ToUnixTimeMilliseconds();
            var keyBytes = Encoding.UTF8.GetBytes(key);

            Directory.CreateDirectory(directory);

            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(expires);
                writer.Write(keyBytes.Length);
                writer.Write(keyBytes);
                writer.Write(value);
            }

            return temp;
        }

        /// <summary>
        /// Reads an entry file.
        /// </summary>
        /// <param name="path">The entry file.</param>
        /// <returns>The entry, or null when the file is absent or not a valid entry.</returns>
        private static (string Key, byte[] Value, long Expires)? Read(string path)
        {
            // a network file system may briefly report a just-replaced file as stale; a retry
            // reaches the new file
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    // shared for deletion so a reader never blocks another instance replacing the
                    // entry - windows refuses to rename over a file opened without it
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);

                    return Parse(buffer.ToArray());
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    return null;
                }
                catch (Exception ex) when (attempt < 2 && ex is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(10);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Decodes the content of an entry file.
        /// </summary>
        /// <param name="content">The raw bytes.</param>
        /// <returns>The entry, or null when the bytes do not form one.</returns>
        private static (string Key, byte[] Value, long Expires)? Parse(byte[] content)
        {
            const int header = 4 + 8 + 4;

            if (content.Length < header || !content.AsSpan(0, 4).SequenceEqual(Magic))
            {
                return null;
            }

            var expires = BitConverter.ToInt64(content, 4);
            var keyLength = BitConverter.ToInt32(content, 12);

            if (keyLength < 0 || header + keyLength > content.Length)
            {
                return null;
            }

            var key = Encoding.UTF8.GetString(content, header, keyLength);
            var value = content.AsSpan(header + keyLength).ToArray();

            return (key, value, expires);
        }

        /// <summary>
        /// Determines whether a deadline lies in the future.
        /// </summary>
        /// <param name="expires">The deadline in unix milliseconds.</param>
        /// <returns>True while the entry counts.</returns>
        private bool IsLive(long expires)
        {
            return expires > _clock.GetUtcNow().ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Starts a sweep once the interval has passed, in the background so the write that
        /// crosses the interval does not wait for it.
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
        /// Returns the file of an entry. The key is hashed so it can never contain a path
        /// separator, whatever a caller passes in.
        /// </summary>
        /// <param name="scope">The group the entry belongs to.</param>
        /// <param name="key">The key of the entry.</param>
        /// <returns>The absolute path of the entry file.</returns>
        private string EntryPath(string scope, string key)
        {
            ArgumentNullException.ThrowIfNull(key);

            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

            return Path.Combine(ScopeDirectory(scope), name + ".entry");
        }

        /// <summary>
        /// Returns the directory of a scope.
        /// </summary>
        /// <param name="scope">The scope, restricted to a plain name so it stays inside the store.</param>
        /// <returns>The absolute path of the directory.</returns>
        private string ScopeDirectory(string scope)
        {
            if (scope is null || !ScopePattern().IsMatch(scope))
            {
                throw new ArgumentException("A cluster scope consists of lowercase letters, digits, '.' and '-'.", nameof(scope));
            }

            return Path.Combine(_directory, scope);
        }

        /// <summary>
        /// Lists the entry files of a scope directory, tolerating its removal mid-listing.
        /// </summary>
        /// <param name="directory">The scope directory.</param>
        /// <returns>The entry files.</returns>
        private static IEnumerable<string> EnumerateEntries(string directory)
        {
            try
            {
                return Directory.GetFiles(directory, "*.entry");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }

        /// <summary>
        /// Deletes a file, ignoring that it is already gone or held by another instance.
        /// </summary>
        /// <param name="path">The file to delete.</param>
        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // the sweep removes it later
            }
        }

        /// <summary>
        /// Matches a valid scope name.
        /// </summary>
        [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,63}$")]
        private static partial Regex ScopePattern();

        /// <summary>
        /// Creates a hard link, failing when the new name already exists.
        /// </summary>
        /// <param name="existing">The file to link to.</param>
        /// <param name="name">The new name.</param>
        /// <returns>Zero on success; otherwise -1 with the error in errno.</returns>
        [DllImport("libc", EntryPoint = "link", SetLastError = true)]
        private static extern int Link(string existing, string name);
    }
}
