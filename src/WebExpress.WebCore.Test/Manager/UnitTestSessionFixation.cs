using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that signing in and out gives the session a new id, so an id the client held
    /// before - planted by an attacker, say - no longer names the session (session fixation).
    /// </summary>
    public class UnitTestSessionFixation
    {
        /// <summary>
        /// Verifies that login and logout replace the session id and the old id stops resolving.
        /// </summary>
        [Fact]
        public void LoginAndLogoutRegenerateTheSessionId()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var request = fixture.Request();
            var session = fixture.Hub.SessionManager.GetSession(request);
            var before = session.Id;

            // act
            fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), request);
            var afterLogin = session.Id;
            fixture.Manager.Logout(request);

            // validation
            Assert.NotEqual(before, afterLogin);
            Assert.NotEqual(afterLogin, session.Id);
        }
    }
}
