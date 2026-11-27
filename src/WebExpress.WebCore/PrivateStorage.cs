using System;
using System.IO;

namespace WebExpress.WebCore
{
    /// <summary>
    /// Creates the files and directories that hold state of the server - sessions, revocation
    /// markers, the package catalog, deployed settings - readable by the account of the server
    /// only.
    /// </summary>
    /// <remarks>
    /// Created with the default mode, on a unix host they were readable by every local account
    /// (0644, directories 0755): session contents, and settings files that carry passwords. On
    /// windows the access rules are inherited from the parent directory, which the deployment
    /// controls, so nothing is changed there.
    /// </remarks>
    internal static class PrivateStorage
    {
        /// <summary>
        /// The mode of a file only the owner may read and write.
        /// </summary>
        private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        /// <summary>
        /// The mode of a directory only the owner may enter.
        /// </summary>
        private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        /// <summary>
        /// Creates a directory, private to the owner when it is new.
        /// </summary>
        /// <param name="path">The directory.</param>
        /// <returns>The directory.</returns>
        internal static DirectoryInfo CreateDirectory(string path)
        {
            return OperatingSystem.IsWindows()
                ? Directory.CreateDirectory(path)
                : Directory.CreateDirectory(path, DirectoryMode);
        }

        /// <summary>
        /// Opens a file for writing, created private to the owner.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="mode">How the file is opened; it must create the file.</param>
        /// <param name="access">The access.</param>
        /// <param name="share">The sharing.</param>
        /// <returns>The stream.</returns>
        internal static FileStream Create(string path, System.IO.FileMode mode, FileAccess access, FileShare share)
        {
            var options = new FileStreamOptions
            {
                Mode = mode,
                Access = access,
                Share = share
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = FileMode;
            }

            return new FileStream(path, options);
        }
    }
}
