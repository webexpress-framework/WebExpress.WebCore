using System.Collections.Generic;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Defines the certificate inventory independently of the listeners that consume it.
    /// </summary>
    public sealed class CertificateManagerSettings
    {
        /// <summary>
        /// Gets or sets the base directory for relative PFX references, relative to the working directory.
        /// </summary>
        public string Directory { get; set; } = ".";

        /// <summary>
        /// Gets or sets the remaining lifetime thresholds in days that trigger expiry warnings.
        /// A missing value uses 30, 14 and 7 days; an empty array disables expiry warnings.
        /// </summary>
        public int[] WarningThresholdDays { get; set; }

        /// <summary>
        /// Gets or sets the certificates that can be shared by listeners and applications.
        /// </summary>
        public List<CertificateSettings> Items { get; set; } = [];
    }
}
