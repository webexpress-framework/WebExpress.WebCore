using System.Collections.Generic;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Exposes the active delivery policy without publishing credentials or provider-specific secrets.
    /// </summary>
    public sealed class EmailStatus
    {
        /// <summary>Gets whether the installation permits email submission.</summary>
        public bool Enabled { get; init; }

        /// <summary>Gets the profile selected when callers omit an explicit profile name.</summary>
        public string DefaultProfile { get; init; }

        /// <summary>Gets the active retention window for delivery claims.</summary>
        public int DeduplicationHours { get; init; }

        /// <summary>Gets the active combined attachment limit before MIME encoding.</summary>
        public long MaxAttachmentBytes { get; init; }

        /// <summary>Gets whether the current deployment participates in a cluster.</summary>
        public bool IsClustered { get; init; }

        /// <summary>Gets whether delivery claims are visible to other instances.</summary>
        public bool IsStoreShared { get; init; }

        /// <summary>Gets the credential-free profile snapshots from the running manager.</summary>
        public IReadOnlyList<EmailProfileInfo> Profiles { get; init; }
    }
}
