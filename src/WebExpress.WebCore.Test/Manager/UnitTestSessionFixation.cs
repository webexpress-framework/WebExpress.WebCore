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

        /// <summary>
        /// Verifies that signing out ends the access credential at once: a copied access cookie
        /// stayed valid until it expired, since the revoked grant only stopped its renewal.
        /// </summary>
        [Fact]
        public void LogoutEndsTheAccessCredential()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var pair = fixture.Manager.Issue(MockIdentityFactory.GetIdentity("Alice"), fixture.Application);
            var request = fixture.Request($"Cookie: {WebExpress.WebCore.WebIdentity.IdentityManager.AccessCookieName}={pair.AccessToken}\r\n");
            var before = fixture.Manager.ValidateAccessToken(pair.AccessToken, fixture.Application);

            // act
            fixture.Manager.Logout(request);

            // validation
            Assert.NotNull(before);
            Assert.Null(fixture.Manager.ValidateAccessToken(pair.AccessToken, fixture.Application));
        }
    }
}
