using System;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Defines the trust boundary shared by every instance serving the same applications.
    /// Secrets must come from deployment configuration and must never be generated per process.
    /// </summary>
    public sealed class AuthenticationSettings
    {
        /// <summary>
        /// Identifies the authority that issues WebExpress tokens.
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Separates this deployment's tokens from those of other services.
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// Supplies at least 256 random bits, encoded as Base64, from a secret store.
        /// </summary>
        public string SigningKey { get; set; }

        /// <summary>
        /// Requires HTTPS and protected cookie prefixes unless explicitly disabled for local development.
        /// Production deployments must retain the default value of true.
        /// </summary>
        public bool RequireHttps { get; set; } = true;

        /// <summary>
        /// Bounds the time a signed authorization snapshot remains usable without a database lookup.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Bounds the entire login grant; rotation never extends this deadline.
        /// </summary>
        public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Limits explicitly created credentials for unattended clients.
        /// </summary>
        public TimeSpan MaximumPersonalAccessTokenLifetime { get; set; } = TimeSpan.FromDays(90);

        /// <summary>
        /// Places refresh replay markers and PAT revocations on durable shared storage.
        /// Used by the default file store for every application without a plugin-supplied store.
        /// </summary>
        public string TokenStorePath { get; set; }

        /// <summary>
        /// Chooses an application for the root authentication endpoints when none is supplied.
        /// </summary>
        public string ApplicationId { get; set; }
    }
}
