using System;
using System.Collections.Generic;

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
        /// Used by the default file store for every application without a plugin-supplied store. Left unset,
        /// the markers go to the cluster store when it is shared (<c>WebExpress:Cluster:StatePath</c> or a
        /// durable store supplied by a plugin).
        /// </summary>
        public string TokenStorePath { get; set; }

        /// <summary>
        /// Chooses an application for the root authentication endpoints when none is supplied.
        /// </summary>
        public string ApplicationId { get; set; }

        /// <summary>
        /// Names groups of applications whose tokens are accepted by each other, so one sign-in
        /// serves all of them. The browser keeps one access cookie for the whole host; without a
        /// group a sign-in to one application would end the sign-in to the other. Grant revocations
        /// and refresh replay markers have to reach every member, so the members share one token
        /// store - the default store does, a plugin-supplied store has to.
        /// </summary>
        /// <example>
        /// "ApplicationGroups": { "kleenestar": [ "kleenestar.core", "kleenestar.portal" ] }
        /// </example>
        public Dictionary<string, List<string>> ApplicationGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
