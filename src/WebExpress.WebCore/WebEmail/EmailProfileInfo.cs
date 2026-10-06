namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Describes an active profile without exposing account names, passwords or arbitrary provider options.
    /// </summary>
    public sealed class EmailProfileInfo
    {
        /// <summary>Gets the name selected by applications.</summary>
        public string Name { get; init; }

        /// <summary>Gets the configured provider name.</summary>
        public string Provider { get; init; }

        /// <summary>Gets the configured default sender.</summary>
        public string From { get; init; }

        /// <summary>Gets the SMTP hostname, or null for a custom provider.</summary>
        public string Host { get; init; }

        /// <summary>Gets the SMTP port, or null for a custom provider.</summary>
        public int? Port { get; init; }

        /// <summary>Gets the SMTP protection mode, or null for a custom provider.</summary>
        public EmailSecurity? Security { get; init; }

        /// <summary>Gets the active submission deadline in seconds.</summary>
        public int TimeoutSeconds { get; init; }

        /// <summary>Gets whether SMTP credentials are configured without disclosing their values.</summary>
        public bool AuthenticationConfigured { get; init; }

        /// <summary>Gets whether the selected provider is currently registered.</summary>
        public bool ProviderRegistered { get; init; }

        /// <summary>Gets whether the profile passes local transport configuration validation.</summary>
        public bool ConfigurationValid { get; init; }
    }
}
