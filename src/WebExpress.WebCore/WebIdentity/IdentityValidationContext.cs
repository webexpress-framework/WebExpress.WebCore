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
        /// Gets the time the token was issued.
        /// </summary>
        public DateTimeOffset IssuedAt { get; init; }
    }
}
