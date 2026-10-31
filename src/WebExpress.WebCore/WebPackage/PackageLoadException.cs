using System;

namespace WebExpress.WebCore.WebPackage
{
    /// <summary>
    /// The failure of a package start whose reason is a package text, so the reason reaches
    /// the reader in their language instead of as the server's exception message.
    /// </summary>
    internal sealed class PackageLoadException : Exception
    {
        /// <summary>
        /// Gets the reason the start failed.
        /// </summary>
        public PackageText Reason { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="reason">The reason the start failed.</param>
        public PackageLoadException(PackageText reason)
            : base(reason.ToString())
        {
            Reason = reason;
        }
    }
}
