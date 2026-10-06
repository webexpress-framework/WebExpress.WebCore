using System.Collections.Generic;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Defines the mail profiles shared by applications in this installation.
    /// </summary>
    public sealed class EmailSettings
    {
        /// <summary>
        /// Gets or sets whether applications may submit mail from this installation.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the profile used when an application does not select one.
        /// </summary>
        public string DefaultProfile { get; set; } = "default";

        /// <summary>
        /// Gets or sets how long an attempted delivery remains protected against duplicate submissions.
        /// </summary>
        public int DeduplicationHours { get; set; } = 168;

        /// <summary>
        /// Gets or sets the maximum combined attachment size before MIME encoding.
        /// </summary>
        public long MaxAttachmentBytes { get; set; } = 25 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the named configurations available to all applications.
        /// </summary>
        public Dictionary<string, EmailProfileSettings> Profiles { get; set; } = [];
    }
}
