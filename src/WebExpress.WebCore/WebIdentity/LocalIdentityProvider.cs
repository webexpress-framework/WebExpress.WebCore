using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Adapts a local user directory to the common login pipeline using salted password hashes.
    /// Applications override the directory queries and may override authentication for their password store.
    /// </summary>
    public abstract class LocalIdentityProvider : IIdentityProvider
    {
        private readonly PasswordHasher<IIdentity> _hasher = new();
        private readonly string _dummyHash = new PasswordHasher<IIdentity>().HashPassword(null, Guid.NewGuid().ToString());

        /// <summary>
        /// Verifies credentials before any identity is allowed into central token issuance.
        /// Missing users still incur password hashing work to reduce username timing disclosure.
        /// </summary>
        /// <param name="username">The submitted account name to resolve through the local directory.</param>
        /// <param name="password">The password to verify before returning an authenticated identity.</param>
        /// <returns>The verified local identity, or null when the credentials are rejected.</returns>
        public virtual IIdentity Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || username.Length > 256 || password.Length > 4096) { return null; }
            var identity = GetIdentities().FirstOrDefault(x => string.Equals(x.Name, username, StringComparison.OrdinalIgnoreCase));
            try
            {
                var result = _hasher.VerifyHashedPassword(identity, identity?.PasswordHash ?? _dummyHash, password);
                return result != PasswordVerificationResult.Failed ? identity : null;
            }
            catch (FormatException) { return null; }
        }

        /// <summary>
        /// Supplies the local user directory whose password verifiers this provider is trusted to check.
        /// </summary>
        /// <returns>The local directory identities whose credentials this provider can verify.</returns>
        public abstract IEnumerable<IIdentity> GetIdentities();
        /// <summary>
        /// Allows applications to expose local role groups alongside their user directory.
        /// </summary>
        /// <returns>The local directory groups, or an empty collection when no directory is exposed.</returns>
        public virtual IEnumerable<IIdentityGroup> GetGroups() => [];
        /// <summary>
        /// Leaves interactive login presentation to the application unless a provider overrides it.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="initiator">The protected page that initiated authentication or denied access.</param>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <returns>The provider response, or null when the application should choose the login presentation.</returns>
        public virtual IResponse CreateAuthenticationPrompt(IRequest request, IPageContext initiator, IIdentity identity) => null;
        /// <summary>
        /// Leaves permission-denied presentation to the application unless a provider overrides it.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="initiator">The protected page that initiated authentication or denied access.</param>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <returns>The provider response, or null when the application should choose the denied-access presentation.</returns>
        public virtual IResponse CreateForbiddenResponse(IRequest request, IPageContext initiator, IIdentity identity) => null;
    }
}
