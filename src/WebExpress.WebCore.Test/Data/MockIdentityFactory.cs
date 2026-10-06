using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test.Data
{
    /// <summary>
    /// A factory class for creating mock identities for test users and groups.
    /// </summary>
    internal class MockIdentityFactory
    {
        private static readonly IEnumerable<MockIdentityGroup> _groups = CreateTestGroups();
        private static readonly IEnumerable<MockIdentity> _users = CreateTestUsers();

        /// <summary>
        /// Retrieves a mock identity based on the provided name.
        /// </summary>
        /// <param name="name">The name of the identity to retrieve.</param>
        /// <returns>The mock identity with the specified name, or null if not found.</returns>
        public static IIdentity GetIdentity(string name)
        {
            return _users.FirstOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Retrieves a mock identitygroup based on the provided name.
        /// </summary>
        /// <param name="name">The name of the identity group to retrieve.</param>
        /// <returns>The mock identity group with the specified name, or null if not found.</returns>
        public static IIdentityGroup GetIdentityGroup(string name)
        {
            return _groups.FirstOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Creates a list of test groups with mock identities.
        /// </summary>
        /// <returns>A list of identity groups.</returns>
        private static IEnumerable<MockIdentityGroup> CreateTestGroups()
        {
            var group1 = new MockIdentityGroup(id: Guid.NewGuid(), name: "Admins");
            group1.Assign([new TestIdentityPolicyA(), new TestIdentityPolicyB()]);

            yield return group1;

            var group2 = new MockIdentityGroup(id: Guid.NewGuid(), name: "Users");
            group2.Assign([new TestIdentityPolicyB()]);

            yield return group2;

            var group3 = new MockIdentityGroup(id: Guid.NewGuid(), name: "Guests");
            group3.Assign([]);

            yield return group3;
        }

        /// <summary>
        /// Creates a list of test users with mock identities.
        /// </summary>
        /// <returns>A list of identities.</returns>
        private static IEnumerable<MockIdentity> CreateTestUsers()
        {
            const string password = "abc";

            var user = new MockIdentity(Guid.NewGuid(), "Alice", "alice@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(0)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Bob", "bob@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Charlie", "charlie@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(2)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "David", "david@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1), _groups.ElementAt(2)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Eve", "eve@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Frank", "frank@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Grace", "grace@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Heidi", "heidi@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Ivan", "ivan@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1)]);

            yield return user;

            user = new MockIdentity(Guid.NewGuid(), "Judy", "judy@example.com", IdentityManager.HashPassword(password));
            user.Assign([_groups.ElementAt(1)]);

            yield return user;
        }
    }
}
