using System;
using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Represents an identity in the web application.
    /// </summary>
    public interface IIdentity
    {
        /// <summary>
        /// Gets the id of the identity.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the name of the identity.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the email of the identity.
        /// </summary>
        public string Email { get; }

        /// <summary>
        /// Gets the hash of the password.
        /// </summary>
        string PasswordHash { get; }

        /// <summary>
        /// Gets the groups associated with the identity.
        /// </summary>
        IEnumerable<IIdentityGroup> Groups { get; }

        /// <summary>
        /// Preserves provider role names without requiring the provider on subsequent requests.
        /// </summary>
        IEnumerable<string> Roles => (Groups ?? []).Select(x => x.Name);

        /// <summary>
        /// Carries explicit permission identifiers in credential-free authorization snapshots.
        /// </summary>
        IEnumerable<string> Permissions => [];

        /// <summary>
        /// Preserves policy identifiers without deserializing executable CLR types from a token.
        /// </summary>
        IEnumerable<string> PolicyNames => (Groups ?? []).SelectMany(x => x.Policies ?? [])
            .Select(x => x.GetType().FullName);
    }
}
