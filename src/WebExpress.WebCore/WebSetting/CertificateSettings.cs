namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Identifies certificate material without coupling consumers to its storage technology.
    /// </summary>
    public sealed class CertificateSettings
    {
        /// <summary>
        /// Gets or sets the unique, case-insensitive name used by consumers.
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Gets or sets the concrete DNS names or IP addresses that must be covered by the certificate.
        /// </summary>
        public string[] HostNames { get; set; } = [];

        /// <summary>
        /// Gets or sets the registered store responsible for interpreting the reference.
        /// </summary>
        public string Store { get; set; } = "file";

        /// <summary>
        /// Gets or sets the store-specific reference, which is a PFX path for the file store.
        /// </summary>
        public string Reference { get; set; }

        /// <summary>
        /// Gets or sets the optional credential passed only to the store that loads the material.
        /// </summary>
        public string Password { get; set; }
    }
}
