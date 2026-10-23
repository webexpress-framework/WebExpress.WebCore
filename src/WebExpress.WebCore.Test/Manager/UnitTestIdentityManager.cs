using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the identity manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestIdentityManager
    {
        /// <summary>
        /// Test the register function of the identity manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act & validation
            Assert.Equal(9, componentHub.IdentityManager.Permissions.Count());
            Assert.Equal(6, componentHub.IdentityManager.Policies.Count());
        }

        /// <summary>
        /// Test the remove function of the identity manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var identityManager = componentHub.IdentityManager as IdentityManager;

            // act
            identityManager.Remove(plugin);

            // validation
            Assert.Empty(componentHub.IdentityManager.Permissions);
            Assert.Empty(componentHub.IdentityManager.Policies);
        }

        /// <summary>
        /// Tests whether the identity manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act & validation
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.IdentityManager.GetType()));
        }

        /// <summary>
        /// Test the CheckAccess function of the identity manager.
        /// </summary>
        /// <param name="application">The application whose permission bindings are evaluated.</param>
        /// <param name="identityName">The identity selected for the authorization scenario.</param>
        /// <param name="permission">The required permission type.</param>
        /// <param name="expected">The expected authorization result.</param>
        [Theory]
        [InlineData(typeof(TestApplicationA), "Alice", typeof(TestIdentityPermissionA), true)]
        [InlineData(typeof(TestApplicationA), "Alice", typeof(TestIdentityPermissionB), true)]
        [InlineData(typeof(TestApplicationA), "Alice", typeof(TestIdentityPermissionC), true)]
        [InlineData(typeof(TestApplicationA), "Bob", typeof(TestIdentityPermissionA), true)]
        [InlineData(typeof(TestApplicationA), "Bob", typeof(TestIdentityPermissionB), true)]
        [InlineData(typeof(TestApplicationA), "Bob", typeof(TestIdentityPermissionC), false)]
        [InlineData(typeof(TestApplicationA), "Charlie", typeof(TestIdentityPermissionA), false)]
        [InlineData(typeof(TestApplicationA), "Charlie", typeof(TestIdentityPermissionB), false)]
        [InlineData(typeof(TestApplicationA), "Charlie", typeof(TestIdentityPermissionC), false)]
        public void CheckAccessIdentity(Type application, string identityName, Type permission, bool expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var identityManager = componentHub.IdentityManager as IdentityManager;
            var applicationContext = componentHub.ApplicationManager.GetApplications(application).FirstOrDefault();
            var identity = MockIdentityFactory.GetIdentity(identityName);

            // act
            var access = identityManager.CheckAccess(applicationContext, identity, permission);

            // validation
            Assert.Equal(expected, access);
        }

        /// <summary>
        /// Test the CheckAccess function of the identity manager.
        /// </summary>
        /// <param name="application">The application whose permission bindings are evaluated.</param>
        /// <param name="groupName">The group selected for the authorization scenario.</param>
        /// <param name="permission">The required permission type.</param>
        /// <param name="expected">The expected authorization result.</param>
        [Theory]
        [InlineData(typeof(TestApplicationA), "Admins", typeof(TestIdentityPermissionA), true)]
        [InlineData(typeof(TestApplicationA), "Admins", typeof(TestIdentityPermissionB), true)]
        [InlineData(typeof(TestApplicationA), "Admins", typeof(TestIdentityPermissionC), true)]
        [InlineData(typeof(TestApplicationA), "Users", typeof(TestIdentityPermissionA), true)]
        [InlineData(typeof(TestApplicationA), "Users", typeof(TestIdentityPermissionB), true)]
        [InlineData(typeof(TestApplicationA), "Users", typeof(TestIdentityPermissionC), false)]
        [InlineData(typeof(TestApplicationA), "Guests", typeof(TestIdentityPermissionA), false)]
        [InlineData(typeof(TestApplicationA), "Guests", typeof(TestIdentityPermissionB), false)]
        [InlineData(typeof(TestApplicationA), "Guests", typeof(TestIdentityPermissionC), false)]
        public void CheckAccessGroup(Type application, string groupName, Type permission, bool expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var identityManager = componentHub.IdentityManager as IdentityManager;
            var applicationContext = componentHub.ApplicationManager.GetApplications(application).FirstOrDefault();
            var group = MockIdentityFactory.GetIdentityGroup(groupName);

            // act
            var access = identityManager.CheckAccess(applicationContext, group, permission);

            // validation
            Assert.Equal(expected, access);
        }

        /// <summary>
        /// Test the CheckAccess function of the identity manager.
        /// </summary>
        /// <param name="application">The application whose permission bindings are evaluated.</param>
        /// <param name="policy">The policy type whose permission binding is evaluated.</param>
        /// <param name="permission">The required permission type.</param>
        /// <param name="expected">The expected authorization result.</param>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestIdentityPolicyA), typeof(TestIdentityPermissionA), true)]
        [InlineData(typeof(TestApplicationA), typeof(TestIdentityPolicyA), typeof(TestIdentityPermissionB), true)]
        [InlineData(typeof(TestApplicationA), typeof(TestIdentityPolicyA), typeof(TestIdentityPermissionC), true)]
        [InlineData(typeof(TestApplicationA), typeof(TestIdentityPolicyB), typeof(TestIdentityPermissionA), true)]
        [InlineData(typeof(TestApplicationA), typeof(TestIdentityPolicyB), typeof(TestIdentityPermissionB), true)]
        [InlineData(typeof(TestApplicationA), typeof(TestIdentityPolicyB), typeof(TestIdentityPermissionC), false)]
        public void CheckAccess(Type application, Type policy, Type permission, bool expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var identityManager = componentHub.IdentityManager as IdentityManager;
            var applicationContext = componentHub.ApplicationManager.GetApplications(application).FirstOrDefault();

            // act
            var access = identityManager.CheckAccess(applicationContext, policy, permission);

            // validation
            Assert.Equal(expected, access);
        }

        /// <summary>
        /// Test that the IIdentityGroup interface has the Id and Name properties.
        /// </summary>
        [Fact]
        public void IIdentityGroupHasIdAndName()
        {
            // arrange
            var group = MockIdentityFactory.GetIdentityGroup("Admins");

            // act & validation
            Assert.NotNull(group);
            Assert.IsType<IIdentityGroup>(group, exactMatch: false);
            Assert.NotEqual(Guid.Empty, group.Id);
            Assert.Equal("Admins", group.Name);
        }

        /// <summary>
        /// Tests that an identity provider can be registered and that its identities
        /// are returned by the IdentityManager for the given application context.
        /// </summary>
        [Fact]
        public void RegisterIdentityProvider()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var identityManager = componentHub.IdentityManager as IdentityManager;
            var applicationContext = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault();
            var provider = new MockIdentityProvider();
            var identity = MockIdentityFactory.GetIdentity("alice@example.com");

            provider.Identities.Add(identity);

            // act
            componentHub.IdentityProviderManager.Register(provider, applicationContext);
            var identities = identityManager.GetIdentities(applicationContext).ToList();

            // validation
            Assert.Contains(identity, identities);
            Assert.Single(identities);
        }

        /// <summary>
        /// Tests that an identity provider can be unregistered and that its identities
        /// are no longer returned by the IdentityManager for the given application context.
        /// </summary>
        [Fact]
        public void UnregisterIdentityProvider()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var identityManager = componentHub.IdentityManager as IdentityManager;
            var applicationContext = componentHub.ApplicationManager
                .GetApplications(typeof(TestApplicationA))
                .FirstOrDefault();

            var provider = new MockIdentityProvider();
            var identity = MockIdentityFactory.GetIdentity("alice@example.com");

            provider.Identities.Add(identity);

            componentHub.IdentityProviderManager.Register(provider, applicationContext);

            var identitiesBefore = identityManager.GetIdentities(applicationContext).ToList();
            Assert.Contains(identity, identitiesBefore);
            Assert.Single(identitiesBefore);

            // act
            var removed = componentHub.IdentityProviderManager.Unregister(provider, applicationContext);
            var identitiesAfter = identityManager.GetIdentities(applicationContext).ToList();

            // validation
            Assert.True(removed);
            Assert.DoesNotContain(identity, identitiesAfter);
            Assert.Empty(identitiesAfter);
        }
    }
}
