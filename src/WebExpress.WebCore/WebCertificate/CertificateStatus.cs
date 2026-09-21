using System;

namespace WebExpress.WebCore.WebCertificate
{
    /// <summary>
    /// Describes local TLS suitability independently of client trust and revocation services.
    /// </summary>
    [Flags]
    public enum CertificateStatus
    {
        /// <summary>
        /// Indicates that all local suitability checks passed.
        /// </summary>
        Valid = 0,

        /// <summary>
        /// Indicates that a warning threshold was reached without preventing use.
        /// </summary>
        ExpiringSoon = 1,

        /// <summary>
        /// Prevents use after the certificate validity period.
        /// </summary>
        Expired = 2,

        /// <summary>
        /// Prevents use before the certificate validity period.
        /// </summary>
        NotYetValid = 4,

        /// <summary>
        /// Prevents use when the server cannot prove possession of the private key.
        /// </summary>
        MissingPrivateKey = 8,

        /// <summary>
        /// Prevents use of CA certificates or certificates whose usages exclude TLS server authentication.
        /// </summary>
        InvalidUsage = 16,

        /// <summary>
        /// Prevents use when a configured hostname is not covered by a subject alternative name.
        /// </summary>
        HostNameMismatch = 32,

        /// <summary>
        /// Indicates that the store could not provide readable certificate material.
        /// </summary>
        LoadFailed = 64
    }
}
