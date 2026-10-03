using System.Collections.Generic;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Pins the external authority and maps its trusted roles into local permissions explicitly.
    /// </summary>
    public sealed class OpenIdConnectSettings
    {
        /// <summary>
        /// Distinguishes this provider when multiple external authorities serve one application.
        /// </summary>
        public string ProviderId { get; set; }

        /// <summary>
        /// Pins the HTTPS issuer advertised by discovery and by every accepted ID token.
        /// </summary>
        public string Authority { get; set; }

        /// <summary>
        /// Binds token audiences and authorization requests to the registered relying party.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Authenticates a confidential client at the token endpoint when required by the provider.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Pins the registered HTTPS callback including the application and provider query parameters.
        /// </summary>
        public string RedirectUri { get; set; }

        /// <summary>
        /// Prevents unrecognized external roles from granting local application permissions.
        /// </summary>
        public Dictionary<string, string[]> RolePermissions { get; set; } = [];

        /// <summary>
        /// Explicitly maps external roles to the full names of locally defined policy classes.
        /// </summary>
        public Dictionary<string, string[]> RolePolicies { get; set; } = [];
    }
}
