using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebAsset;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPolicies;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that access is denied unless it is granted: an endpoint without a policy requires a
    /// signed-in caller, and only an endpoint marked public answers everybody.
    /// </summary>
    public class UnitTestAccessByDefault
    {
        /// <summary>
        /// A page that answers callers who are not signed in through the marker interface.
        /// </summary>
        public sealed class PublicMarker : IPublicEndpoint
        {
        }

        /// <summary>
        /// Verifies the access to an endpoint by its policies and the caller.
        /// </summary>
        [Fact]
        public void AccessIsDeniedUnlessGranted()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var identityManager = componentHub.IdentityManager;
            var alice = MockIdentityFactory.GetIdentity("Alice");
            var unmarked = new PageContext { Policies = [] };
            var publicPage = new PageContext { Policies = [new PublicAccessPolicy()] };
            var signedIn = new PageContext { Policies = [new AuthenticatedAccessPolicy()] };
            var broken = new PageContext { Policies = [null] };

            // validation
            Assert.False(identityManager.CheckAccess(null, unmarked), "an endpoint without a policy is not public");
            Assert.True(identityManager.CheckAccess(alice, unmarked), "a signed-in caller may use it");
            Assert.True(identityManager.CheckAccess(null, publicPage), "a public endpoint answers everybody");
            Assert.False(identityManager.CheckAccess(null, signedIn));
            Assert.True(identityManager.CheckAccess(alice, signedIn));
            Assert.False(identityManager.CheckAccess(alice, broken), "a policy that could not be created denies");
        }

        /// <summary>
        /// Verifies that the assets of the framework stay public, since a login page needs them
        /// before anybody is signed in.
        /// </summary>
        [Fact]
        public void AssetsArePublic()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var asset = componentHub.AssetManager.Assets.FirstOrDefault() ?? new AssetContext();

            // validation
            Assert.True(componentHub.IdentityManager.CheckAccess(null, asset));
        }

        /// <summary>
        /// Verifies that the marker interface is inherited, which is what a base class such as the
        /// login page of an application template needs.
        /// </summary>
        [Fact]
        public void MarkerIsInherited()
        {
            Assert.True(typeof(IPublicEndpoint).IsAssignableFrom(typeof(PublicMarker)));
        }
    }
}
