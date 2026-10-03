using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test.Data
{
    /// <summary>
    /// Represents a group identity with an ID and a name.
    /// </summary>
    internal class MockIdentityGroup : IIdentityGroup
    {
        private readonly List<IIdentityPolicy> _policies = [];

        /// <summary>
        /// Gets or sets the id of the group.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the policies associated with the group.
        /// </summary>
        public IEnumerable<IIdentityPolicy> Policies => _policies;

        /// <summary>
        /// Initializes a new instance of the class with the specified id and name.
        /// </summary>
        /// <param name="id">The id of the group.</param>
        /// <param name="name">The name of the group.</param>
        public MockIdentityGroup(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// Assigns policies.
        /// </summary>
        /// <param name="policies">The list of policies to assign to the group.</param>
        public void Assign(IEnumerable<IIdentityPolicy> policies)
        {
            _policies.AddRange(policies);
        }
    }
}
