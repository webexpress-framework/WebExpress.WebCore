using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests the two ways a signed identity is reconsidered beyond its signature: application
    /// groups that accept each other's tokens, and validators asked on every request.
    /// </summary>
    public class UnitTestIdentityValidation
    {
        /// <summary>
        /// Tests that one sign-in serves every application of its group, and no other application.
        /// </summary>
        [Fact]
        public void GroupedApplicationsAcceptEachOthersTokens()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var a = fixture.Application;
            var b = Application(fixture, typeof(TestApplicationB));
            var c = Application(fixture, typeof(TestApplicationC));
            fixture.Groups["suite"] = [a.ApplicationId, b.ApplicationId];
            var manager = fixture.Instance();
            var pair = manager.Issue(new Identity(Guid.NewGuid(), "alice"), a);
            var request = fixture.Request($"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n");
            request.ApplicationContext = b;

            // act
            var inGroup = manager.ValidateAccessToken(pair.AccessToken, b);
            var outsideGroup = manager.ValidateAccessToken(pair.AccessToken, c);
            var current = manager.GetCurrentIdentity(request);
            var renewed = manager.Refresh(pair.RefreshToken, b);
            var renewedAgain = manager.Refresh(pair.RefreshToken, a);

            // validation
            Assert.NotNull(inGroup);
            Assert.Null(outsideGroup);
            Assert.Equal("alice", current?.Name);
            Assert.NotNull(renewed);
            Assert.NotNull(manager.ValidateAccessToken(renewed.AccessToken, a));
            Assert.Null(renewedAgain);
        }

        /// <summary>
        /// Tests that a renewal in a member of two overlapping groups keeps the trust boundary of
        /// the sign-in: a token issued in A and renewed in B is not accepted by C, which trusts B but not A.
        /// </summary>
        [Fact]
        public void RenewalKeepsTheIssuingApplication()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var a = fixture.Application;
            var b = Application(fixture, typeof(TestApplicationB));
            var c = Application(fixture, typeof(TestApplicationC));
            fixture.Groups["ab"] = [a.ApplicationId, b.ApplicationId];
            fixture.Groups["bc"] = [b.ApplicationId, c.ApplicationId];
            var manager = fixture.Instance();
            var pair = manager.Issue(new Identity(Guid.NewGuid(), "alice"), a);

            // act
            var renewed = manager.Refresh(pair.RefreshToken, b);

            // validation
            Assert.Null(manager.ValidateAccessToken(pair.AccessToken, c));
            Assert.NotNull(renewed);
            Assert.NotNull(manager.ValidateAccessToken(renewed.AccessToken, a));
            Assert.NotNull(manager.ValidateAccessToken(renewed.AccessToken, b));
            Assert.Null(manager.ValidateAccessToken(renewed.AccessToken, c));
            Assert.Null(manager.Refresh(renewed.RefreshToken, c));
        }

        /// <summary>
        /// Tests that without a group a token stays bound to the application it was issued in.
        /// </summary>
        [Fact]
        public void UngroupedApplicationsKeepTheirTokens()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var b = Application(fixture, typeof(TestApplicationB));
            var pair = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), fixture.Application);

            // act
            var identity = fixture.Manager.ValidateAccessToken(pair.AccessToken, b);

            // validation
            Assert.Null(identity);
        }

        /// <summary>
        /// Tests that a validator is asked on every request, sees the grant of the sign-in, and
        /// turns a refused identity into an anonymous request, also when it throws.
        /// </summary>
        [Fact]
        public void ValidatorDecidesOnEveryRequest()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var pair = fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), fixture.Request());
            var cookie = $"Cookie: {IdentityManager.AccessCookieName}={pair.AccessToken}\r\n";
            var validator = new DelegateValidator(_ => true);
            fixture.Manager.RegisterValidator(validator, fixture.Application);

            // act
            var accepted = fixture.Manager.GetCurrentIdentity(fixture.Request(cookie));
            var context = validator.Last;
            validator.Answer = _ => false;
            var refused = fixture.Manager.GetCurrentIdentity(fixture.Request(cookie));
            validator.Answer = _ => throw new InvalidOperationException("directory offline");
            var failed = fixture.Manager.GetCurrentIdentity(fixture.Request(cookie));
            var unregistered = fixture.Manager.UnregisterValidator(validator, fixture.Application);
            var afterwards = fixture.Manager.GetCurrentIdentity(fixture.Request(cookie));

            // validation
            Assert.NotNull(accepted);
            Assert.Equal("access", context.TokenUse);
            Assert.False(string.IsNullOrEmpty(context.Grant));
            Assert.Same(fixture.Application, context.ApplicationContext);
            Assert.Null(refused);
            Assert.Null(failed);
            Assert.True(unregistered);
            Assert.NotNull(afterwards);
        }

        /// <summary>
        /// Tests that a refused identity is not renewed and its cookies are removed.
        /// </summary>
        [Fact]
        public void RefusedIdentityIsNotRenewed()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var pair = fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), fixture.Request());
            fixture.Manager.RegisterValidator(new DelegateValidator(_ => false), fixture.Application);
            var request = fixture.Request($"Cookie: {IdentityManager.RefreshCookieName}={pair.RefreshToken}\r\n", "POST", IdentityManager.RefreshPath);
            var response = new ResponseOK();

            // act
            var renewed = fixture.Manager.Refresh(request);
            fixture.Manager.ApplyAuthenticationCookies(request, response);

            // validation
            Assert.Null(renewed);
            Assert.Equal("", response.Header.Cookies[IdentityManager.AccessCookieName]?.Value);
        }

        /// <summary>
        /// Tests that a validator a plugin defines is discovered for the plugin's applications.
        /// </summary>
        [Fact]
        public void PluginValidatorIsDiscovered()
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var blocked = fixture.Manager.Issue(new Identity(Guid.NewGuid(), TestIdentityValidator.BlockedName), fixture.Application).AccessToken;
            var allowed = fixture.Manager.Issue(new Identity(Guid.NewGuid(), "alice"), fixture.Application).AccessToken;

            // act
            var blockedIdentity = fixture.Manager.GetCurrentIdentity(fixture.Request($"Cookie: {IdentityManager.AccessCookieName}={blocked}\r\n"));
            var allowedIdentity = fixture.Manager.GetCurrentIdentity(fixture.Request($"Cookie: {IdentityManager.AccessCookieName}={allowed}\r\n"));

            // validation
            Assert.Null(blockedIdentity);
            Assert.NotNull(allowedIdentity);
        }

        /// <summary>
        /// Returns the application of a type.
        /// </summary>
        /// <param name="fixture">The fixture whose hub knows the applications.</param>
        /// <param name="type">The application type.</param>
        /// <returns>The application context.</returns>
        private static IApplicationContext Application(AuthenticationFixture fixture, Type type)
        {
            return fixture.Hub.ApplicationManager.GetApplications(type).First();
        }

        /// <summary>
        /// A validator whose answer the test sets.
        /// </summary>
        /// <param name="answer">The first answer.</param>
        private sealed class DelegateValidator(Func<IdentityValidationContext, bool> answer) : IIdentityValidator
        {
            /// <summary>
            /// Gets or sets the answer.
            /// </summary>
            public Func<IdentityValidationContext, bool> Answer { get; set; } = answer;

            /// <summary>
            /// Gets the context of the last question.
            /// </summary>
            public IdentityValidationContext Last { get; private set; }

            /// <summary>
            /// Records the question and answers it.
            /// </summary>
            /// <param name="context">The identity to confirm.</param>
            /// <returns>The answer the test set.</returns>
            public bool Validate(IdentityValidationContext context)
            {
                Last = context;

                return Answer(context);
            }
        }
    }
}
