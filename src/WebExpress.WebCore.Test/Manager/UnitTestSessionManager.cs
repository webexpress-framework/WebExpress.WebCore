using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebSession;
using WebExpress.WebCore.WebSession.Model;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the session manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSessionManager
    {
        /// <summary>
        /// Test the register function of the session manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.NotNull(componentHub.SessionManager);
        }

        /// <summary>
        /// Tests whether the session manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.SessionManager.GetType()));
        }

        /// <summary>
        /// Test the GetSession function of the session manager.
        /// </summary>
        [Fact]
        public void GetSession()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock();

            // act
            var session = componentHub.SessionManager.GetSession(request);

            Assert.NotNull(session);
        }

        /// <summary>
        /// Test the SetProperty function of the session manager.
        /// </summary>
        [Fact]
        public void AddPropertyToSession()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock();
            var session = componentHub.SessionManager.GetSession(request);

            // act
            session.SetProperty(new SessionPropertyParameter(new Parameter("test", "test param", ParameterScope.Session)));

            var testProperty = session.GetProperty<SessionPropertyParameter>();

            Assert.NotNull(testProperty);
            Assert.Single(testProperty.Params);
            Assert.Equal("test param", testProperty.Params["test"].Value);
            Assert.Equal(ParameterScope.Session, testProperty.Params["test"].Scope);
        }

        /// <summary>
        /// Test the RemoveProperty function of the session manager.
        /// </summary>
        [Fact]
        public void RemovePropertyFromSession()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock();
            var session = componentHub.SessionManager.GetSession(request);

            // act
            session.SetProperty(new SessionPropertyParameter(new Parameter("test", "test param", ParameterScope.Session)));
            session.RemoveProperty<SessionPropertyParameter>();

            var testProperty = session.GetProperty<SessionPropertyParameter>();

            Assert.Null(testProperty);
        }

        /// <summary>
        /// A well-formed id the server never issued must not become a session id: a client
        /// that could dictate the id would plant it in a victim's browser and read the
        /// victim's session once they signed in.
        /// </summary>
        [Fact]
        public void GetSession_UnknownCookieId_IsNotAdopted()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var planted = Guid.NewGuid();
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={planted}\n\n");

            // act
            var session = componentHub.SessionManager.GetSession(request);

            // validation
            Assert.NotNull(session);
            Assert.NotEqual(planted, session.Id);
            Assert.NotEqual(Guid.Empty, session.Id);
        }

        /// <summary>
        /// A request without a session cookie gets a fresh session.
        /// </summary>
        [Fact]
        public void GetSession_NoCookie_CreatesSession()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock("GET / HTTP/1.1\nCookie:\n\n");

            // act
            var session = componentHub.SessionManager.GetSession(request);

            // validation
            Assert.NotNull(session);
            Assert.NotEqual(Guid.Empty, session.Id);
        }

        /// <summary>
        /// A cookie naming an id the server issued resolves to that session.
        /// </summary>
        [Fact]
        public void GetSession_KnownCookieId_ReturnsThatSession()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var issued = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={issued.Id}\n\n");

            // act
            var session = componentHub.SessionManager.GetSession(request);

            // validation
            Assert.Same(issued, session);
        }

        /// <summary>
        /// The id of a session created for a request only reaches the client with the response,
        /// so every lookup made while answering that request - the request itself, the sign-in,
        /// the cookie - must find the same session even though no cookie can name it yet.
        /// Otherwise the identity would be bound to one session and the client sent the id of
        /// another.
        /// </summary>
        /// <param name="cookie">The cookie header the request carries.</param>
        [Theory]
        [InlineData("")]
        [InlineData("session=")]
        [InlineData("session=not-a-guid")]
        [InlineData("session=6F9619FF-8B86-D011-B42D-00C04FC964FF")]
        public void GetSession_SameRequest_ReturnsSameSession(string cookie)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: {cookie}\n\n");

            // act
            var first = componentHub.SessionManager.GetSession(request);
            var second = componentHub.SessionManager.GetSession(request);

            // validation
            Assert.NotNull(first);
            Assert.Same(first, second);
            Assert.Same(request.Session, first);
        }

        /// <summary>
        /// Regenerating the id keeps the session's state, makes the new id resolve to it and
        /// retires the old one - whoever holds the old id holds nothing.
        /// </summary>
        [Fact]
        public void RegenerateId_ReplacesIdKeepsStateAndRetiresOldId()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var session = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            var oldId = session.Id;
            session.SetProperty(new SessionPropertyParameter(new Parameter("test", "test param", ParameterScope.Session)));

            // act
            var newId = componentHub.SessionManager.RegenerateId(session);

            // validation
            Assert.NotEqual(oldId, newId);
            Assert.Equal(newId, session.Id);
            Assert.NotNull(session.GetProperty<SessionPropertyParameter>());

            var withOldId = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={oldId}\n\n");
            Assert.NotSame(session, componentHub.SessionManager.GetSession(withOldId));

            var withNewId = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={newId}\n\n");
            Assert.Same(session, componentHub.SessionManager.GetSession(withNewId));
        }

        /// <summary>
        /// Regenerating the id of a session the manager has never seen still registers it
        /// under the new id, so a caller need not know where the session came from.
        /// </summary>
        [Fact]
        public void RegenerateId_UnknownSession_RegistersIt()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var session = new Session();

            // act
            var newId = componentHub.SessionManager.RegenerateId(session);

            // validation
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={newId}\n\n");
            Assert.Same(session, componentHub.SessionManager.GetSession(request));
        }

        /// <summary>
        /// Regenerating without a session is a programming error and must not pass silently.
        /// </summary>
        [Fact]
        public void RegenerateId_Null_Throws()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act & validation
            Assert.Throws<ArgumentNullException>(() => componentHub.SessionManager.RegenerateId(null));
        }

        /// <summary>
        /// A session left idle past the timeout must not revive on the next request even before
        /// the periodic cleanup runs: its id is dropped and a fresh session takes its place, so a
        /// stale cookie never carries access back.
        /// </summary>
        [Fact]
        public void GetSession_ExpiredSession_IsReplaced()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            componentHub.SessionManager.Timeout = TimeSpan.FromMinutes(30);
            var seeded = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            var staleId = seeded.Id;

            // push the last-access point beyond the timeout
            seeded.Updated = DateTime.Now.AddHours(-1);

            // act
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={staleId}\n\n");
            var session = componentHub.SessionManager.GetSession(request);

            // validation
            Assert.NotEqual(staleId, session.Id);

            // the stale id resolves to yet another fresh session, never back to the expired one
            var again = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={staleId}\n\n");
            Assert.NotEqual(staleId, componentHub.SessionManager.GetSession(again).Id);
        }

        /// <summary>
        /// An active session renews its idle deadline on each access (sliding window), so a user
        /// who keeps using the site is never expired out from under an ongoing session.
        /// </summary>
        [Fact]
        public void GetSession_ActiveSession_RenewsAndPersists()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            componentHub.SessionManager.Timeout = TimeSpan.FromMinutes(30);
            var seeded = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            seeded.Updated = DateTime.Now.AddMinutes(-10);

            // act
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={seeded.Id}\n\n");
            var session = componentHub.SessionManager.GetSession(request);

            // validation
            Assert.Same(seeded, session);
            Assert.True((DateTime.Now - session.Updated).TotalMinutes < 1, "the access renewed the idle deadline");
        }

        /// <summary>
        /// A non-positive timeout means expiry is switched off, so even a long-idle session is
        /// returned unchanged.
        /// </summary>
        [Fact]
        public void GetSession_TimeoutDisabled_NeverExpires()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            componentHub.SessionManager.Timeout = TimeSpan.Zero;
            var seeded = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            seeded.Updated = DateTime.Now.AddDays(-3650);

            // act
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={seeded.Id}\n\n");
            var session = componentHub.SessionManager.GetSession(request);

            // validation
            Assert.Same(seeded, session);
        }

        /// <summary>
        /// The default idle lifetime is finite and modest - neither the year-long window the
        /// cleanup used to assume nor the never-expiring cookie that went with it.
        /// </summary>
        [Fact]
        public void Timeout_DefaultIsBoundedAndModest()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // validation
            Assert.Equal(WebExpress.WebCore.WebSession.SessionManager.DefaultTimeout, componentHub.SessionManager.Timeout);
            Assert.True(componentHub.SessionManager.Timeout > TimeSpan.Zero);
            Assert.True(componentHub.SessionManager.Timeout <= TimeSpan.FromDays(31));
        }

        /// <summary>
        /// With no explicit timeout, cleanup reclaims exactly what the access check would already
        /// refuse - it sweeps by the manager's own <see cref="WebExpress.WebCore.WebSession.SessionManager.Timeout"/>
        /// rather than by the old year-long default. The call goes through the interface on
        /// purpose: a default argument is taken from the type the caller sees, so a default that
        /// only the implementation declares never reaches a caller such as the cleanup job.
        /// </summary>
        [Fact]
        public void CleanUp_WithoutExplicitTimeout_UsesTheManagerTimeout()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            ISessionManager sessionManager = componentHub.SessionManager;
            sessionManager.Timeout = TimeSpan.FromMinutes(30);
            var seeded = sessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            var staleId = seeded.Id;
            seeded.Updated = DateTime.Now.AddHours(-1);
            var applicationContext = new WebExpress.WebCore.WebApplication.ApplicationContext();

            // act
            sessionManager.CleanUp(applicationContext);

            // validation - expiry on access is switched off, so the stale id could only fail to
            // resolve because the sweep removed it, not because the lookup refused it
            sessionManager.Timeout = TimeSpan.Zero;
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={staleId}\n\n");
            Assert.NotEqual(staleId, componentHub.SessionManager.GetSession(request).Id);
        }

        /// <summary>
        /// The session directory is shared mutable state: many requests read and create sessions
        /// at once. Under a single lock the lookups and creations stay consistent - a known id
        /// keeps resolving to its one instance, and no concurrent access corrupts the directory.
        /// </summary>
        [Fact]
        public void GetSession_UnderConcurrency_StaysConsistent()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            componentHub.SessionManager.Timeout = TimeSpan.Zero; // isolate concurrency from expiry
            var known = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            var knownCookie = $"GET / HTTP/1.1\nCookie: session={known.Id}\n\n";

            // act - hammer the manager: half the threads re-read the one known session while the
            // other half force fresh creations, so lookups and inserts race on the directory. The
            // count is high so the many inserts trigger dictionary resizes while reads are in
            // flight - the exact window an unsynchronised read would tear on
            System.Threading.Tasks.Parallel.For(0, 50000, i =>
            {
                if (i % 2 == 0)
                {
                    var resolved = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock(knownCookie));
                    Assert.Same(known, resolved);
                }
                else
                {
                    var fresh = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock("GET / HTTP/1.1\nCookie:\n\n"));
                    Assert.NotNull(fresh);
                }
            });

            // validation - the known session survived the storm intact
            Assert.Same(known, componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock(knownCookie)));
        }
    }
}
