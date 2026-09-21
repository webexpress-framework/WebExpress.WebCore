using System;
using System.Collections.Generic;
using System.Linq;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Carries a credential-free authorization snapshot across provider and token boundaries.
    /// </summary>
    public sealed class Identity : IIdentity
    {
        /// <summary>
        /// Provides the stable subject used to bind signed claims to one user.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Preserves the display name independently of the originating provider.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Preserves the contact claim without requiring a provider lookup.
        /// </summary>
        public string Email { get; }

        /// <summary>
        /// Keeps password verifiers out of identities reconstructed from tokens.
        /// </summary>
        public string PasswordHash => null;

        /// <summary>
        /// Avoids rebuilding executable policy objects from serialized claims.
        /// </summary>
        public IEnumerable<IIdentityGroup> Groups => [];

        /// <summary>
        /// Preserves the trusted role labels captured during authentication.
        /// </summary>
        public IEnumerable<string> Roles { get; }

        /// <summary>
        /// Limits authorization to permission identifiers captured during authentication.
        /// </summary>
        public IEnumerable<string> Permissions { get; }

        /// <summary>
        /// Preserves local policy identifiers without loading types named by a token.
        /// </summary>
        public IEnumerable<string> PolicyNames { get; }

        /// <summary>
        /// Copies trusted claims so provider mutations cannot change an issued identity.
        /// </summary>
        /// <param name="id">The stable subject identifier supplied by the authenticated identity source.</param>
        /// <param name="name">The display name supplied by the authenticated identity source.</param>
        /// <param name="email">The optional contact claim captured from the authenticated source.</param>
        /// <param name="roles">The trusted role labels captured during authentication.</param>
        /// <param name="permissions">The effective permission identifiers granted by the trusted identity source.</param>
        /// <param name="policyNames">The full local policy names granted to the authenticated identity.</param>
        public Identity(Guid id, string name, string email = null, IEnumerable<string> roles = null,
            IEnumerable<string> permissions = null, IEnumerable<string> policyNames = null)
        {
            if (id == Guid.Empty) { throw new ArgumentException("An identity needs a stable subject.", nameof(id)); }
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            Id = id;
            Name = name;
            Email = email;
            Roles = Array.AsReadOnly((roles ?? []).Distinct(StringComparer.Ordinal).ToArray());
            Permissions = Array.AsReadOnly((permissions ?? []).Distinct(StringComparer.Ordinal).ToArray());
            PolicyNames = Array.AsReadOnly((policyNames ?? []).Distinct(StringComparer.Ordinal).ToArray());
        }
    }
}
