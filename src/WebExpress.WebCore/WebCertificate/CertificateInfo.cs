using System;
using System.Collections.Generic;

namespace WebExpress.WebCore.WebCertificate
{
    /// <summary>
    /// Exposes a metadata snapshot without revealing credentials or private key material.
    /// </summary>
    public sealed class CertificateInfo
    {
        /// <summary>
        /// Gets the stable name applications can resolve.
        /// </summary>
        public string Alias { get; internal init; }

        /// <summary>
        /// Gets the configured concrete host mappings.
        /// </summary>
        public IReadOnlyList<string> HostNames { get; internal init; }

        /// <summary>
        /// Gets the store name for operational diagnostics.
        /// </summary>
        public string Store { get; internal init; }

        /// <summary>
        /// Gets the certificate subject, or null if loading failed.
        /// </summary>
        public string Subject { get; internal init; }

        /// <summary>
        /// Gets the certificate issuer, or null if loading failed.
        /// </summary>
        public string Issuer { get; internal init; }

        /// <summary>
        /// Gets the certificate thumbprint for identifying a deployed version.
        /// </summary>
        public string Thumbprint { get; internal init; }

        /// <summary>
        /// Gets the UTC validity start, or null if loading failed.
        /// </summary>
        public DateTimeOffset? NotBefore { get; internal init; }

        /// <summary>
        /// Gets the UTC expiry time, or null if loading failed.
        /// </summary>
        public DateTimeOffset? NotAfter { get; internal init; }

        /// <summary>
        /// Gets the suitability status evaluated when this snapshot was requested.
        /// </summary>
        public CertificateStatus Status { get; internal init; }

        /// <summary>
        /// Gets whether the material can be handed to a TLS listener.
        /// </summary>
        public bool IsUsable => (Status & ~CertificateStatus.ExpiringSoon) == CertificateStatus.Valid;
    }
}
