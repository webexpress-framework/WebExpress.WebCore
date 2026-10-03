using System;
using System.Collections.Generic;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// A named group of identities (users) used for access control. Instead of granting rights to
    /// each user individually, a group bundles a set of policies that apply to all of its members.
    /// </summary>
    public interface IIdentityGroup
    {
        /// <summary>
        /// Gets the id of the group.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the name of the group.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the policies associated with the group.
        /// </summary>
        IEnumerable<IIdentityPolicy> Policies { get; }
    }
}
