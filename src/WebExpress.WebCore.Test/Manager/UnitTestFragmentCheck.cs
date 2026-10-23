using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Guards the visibility check every fragment runs before it renders, so a link to a
    /// protected page is not offered to identities the server would turn away.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestFragmentCheck
    {
        /// <summary>
        /// A fragment without policies stays visible to anonymous requests.
        /// </summary>
        [Fact]
        public void FragmentWithoutPolicyIsShownToAnyone()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var fragmentContext = new FragmentContext();

            // act
            var visible = fragmentContext.Check(fixture.Request());

            // validation
            Assert.True(visible);
        }

        /// <summary>
        /// A fragment with a policy is shown only to an identity that holds the policy.
        /// </summary>
        /// <param name="authenticated">Whether a real signed login supplies the request identity.</param>
        /// <param name="grantPolicy">Whether the login grants the fragment's policy.</param>
        /// <param name="expected">Whether the fragment is expected to be visible.</param>
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void FragmentWithPolicyIsShownOnlyToGrantedIdentity(bool authenticated, bool grantPolicy, bool expected)
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var request = fixture.Request();
            var fragmentContext = new FragmentContext { Policies = [new TestIdentityPolicyA()] };
            if (authenticated)
            {
                fixture.Manager.Login(new Identity(Guid.NewGuid(), "test-user",
                    policyNames: grantPolicy ? [typeof(TestIdentityPolicyA).FullName] : []), request);
            }

            // act
            var visible = fragmentContext.Check(request);

            // validation
            Assert.Equal(expected, visible);
        }

        /// <summary>
        /// A failing condition hides the fragment even from an identity that holds the policy.
        /// </summary>
        [Fact]
        public void FailingConditionHidesFragmentDespitePolicy()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var request = fixture.Request();
            var fragmentContext = new FragmentContext
            {
                Conditions = [new ConditionNever()],
                Policies = [new TestIdentityPolicyA()]
            };
            fixture.Manager.Login(new Identity(Guid.NewGuid(), "test-user",
                policyNames: [typeof(TestIdentityPolicyA).FullName]), request);

            // act
            var visible = fragmentContext.Check(request);

            // validation
            Assert.False(visible);
        }

        /// <summary>
        /// Stands for any condition that is not met, independent of the identity.
        /// </summary>
        private sealed class ConditionNever : WebCondition.ICondition
        {
            /// <summary>
            /// Rejects every request.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <returns>Always false.</returns>
            public bool Fulfillment(WebMessage.IRequest request) => false;
        }
    }
}
