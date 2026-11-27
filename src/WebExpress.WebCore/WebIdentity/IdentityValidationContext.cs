using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// What an <see cref="IIdentityValidator"/> is asked about: the identity a verified token
    /// carries, the login grant the token belongs to and the request it arrived with.
    /// </summary>
    public sealed class IdentityValidationContext
    {
        /// <summary>
        /// Gets the identity the token carries.
        /// </summary>
        public IIdentity Identity { get; init; }

        /// <summary>
        /// Gets the request the token arrived with.
        /// </summary>
        public IRequest Request { get; init; }

        /// <summary>
        /// Gets the application the request is addressed to. It may differ from the application
        /// the token was issued in when both belong to one application group.
        /// </summary>
        public IApplicationContext ApplicationContext { get; init; }

        /// <summary>
        /// Gets the login grant the token belongs to, which every token of one sign-in shares
        /// across renewals; null for a personal access token, which has no grant.
        /// </summary>
        public string Grant { get; init; }

        /// <summary>
        /// Gets the kind of token: "access" for a browser sign-in, "pat" for a personal access token.
        /// </summary>
        public string TokenUse { get; init; }

        /// <summary>
        /// Gets the id (<c>jti</c>) of the token, which a validator matches a personal access
        /// token by; null when the token carries none.
        /// </summary>
        public string TokenId { get; init; }

        /// <summary>
        /// Gets the time the token was issued.
        /// </summary>
        public DateTimeOffset IssuedAt { get; init; }

        /// <summary>
        /// Gets the time the token itself stops being accepted.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>
        /// Gets the time the login grant ends however often it is renewed, which is how long a
        /// revocation of the grant must last; null for a token without a grant.
        /// </summary>
        /// <remarks>
        /// A validator takes the token's claims from here and never from the request: on a
        /// renewal the request still carries the previous access cookie, which nobody verified.
        /// </remarks>
        public DateTimeOffset? GrantExpiresAt { get; init; }
    }
}
