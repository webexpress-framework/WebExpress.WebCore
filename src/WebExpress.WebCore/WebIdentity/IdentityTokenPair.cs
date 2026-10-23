using System;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Transfers newly issued credentials to the HTTP-only cookie writer, never to a JSON response.
    /// </summary>
    public sealed class IdentityTokenPair
    {
        /// <summary>
        /// Preserves the browser persistence choice across credential renewal.
        /// </summary>
        public bool RememberMe { get; internal init; } = true;

        /// <summary>
        /// Authorizes requests until the short-lived snapshot expires.
        /// </summary>
        public string AccessToken { get; internal init; }

        /// <summary>
        /// Permits one renewal at the dedicated refresh endpoint.
        /// </summary>
        public string RefreshToken { get; internal init; }

        /// <summary>
        /// Tells clients when the signed authorization expires and renewal is required.
        /// </summary>
        public DateTimeOffset AccessTokenExpiresAt { get; internal init; }

        /// <summary>
        /// Keeps the refresh cookie within the original login grant's deadline.
        /// </summary>
        public DateTimeOffset RefreshTokenExpiresAt { get; internal init; }
    }
}
