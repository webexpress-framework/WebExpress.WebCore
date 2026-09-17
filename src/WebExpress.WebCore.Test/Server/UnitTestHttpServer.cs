using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Test the HTTP server.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHttpServer
    {
        // a policy-free endpoint, so the request is answered by its handler and nothing
        // in between - the access check, the login prompt - decides the outcome
        private const string Endpoint = "/server/appa/api/2/testrestapib";

        /// <summary>
        /// Answers a request through the server and returns what the client would see of it.
        /// </summary>
        /// <remarks>
        /// The fixture's context carries no response features, so the sender would log and
        /// give up before writing a header; they are added here so the Set-Cookie header the
        /// server emits can be read back.
        /// </remarks>
        /// <param name="componentHub">The hub the server answers on behalf of.</param>
        /// <param name="content">The raw request.</param>
        /// <param name="whileAnswering">
        /// What a handler would do with the request - a sign-in, say - before the response leaves.
        /// </param>
        /// <param name="settings">Optional server settings, e.g. to force the cookie's Secure flag.</param>
        /// <returns>The response head and the request as the server materialised it.</returns>
        private static async Task<(HttpResponseFeature Response, WebMessage.IRequest Request)> AnswerAsync(WebComponent.ComponentHub componentHub, string content, Action<WebMessage.IRequest> whileAnswering = null, HttpServerSettings settings = null)
        {
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var httpContext = UnitTestFixture.CreateHttpContextMock(content);
            var responseFeature = new HttpResponseFeature();
            httpContext.Features.Set<IHttpResponseFeature>(responseFeature);
            httpContext.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(new MemoryStream()));
            var server = new HttpServer(httpServerContext) { Settings = settings };
            componentHub.SitemapManager.Refresh();

            whileAnswering?.Invoke(httpContext.Request);

            await server.ProcessRequestAsync(httpContext);

            return (responseFeature, httpContext.Request);
        }
        /// <summary>
        /// Asynchronously processes an HTTP request using the specified HTTP context.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task ProcessRequestAsync()
        {
            // arrange
            var content = "GET /server/appa/api/1/testrestapia HTTP/1.1\n" +
            "Authorization: Bearer abc123\n" +
            "X-Test: 123\n" +
            "X-Mode: UnitTest\n" +
            "\n";
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var httpContext = UnitTestFixture.CreateHttpContextMock(content);
            var server = new HttpServer(httpServerContext);
            componentHub.SitemapManager.Refresh();

            // act
            await server.ProcessRequestAsync(httpContext);

            // validation
            Assert.NotNull(server);
        }

        /// <summary>
        /// A first visit is answered with the id of the session the server created for it -
        /// site-wide, and out of reach of script, since the id is all that authenticates the
        /// client.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_NoSessionCookie_IssuesHttpOnlySessionCookie()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var content = $"GET {Endpoint} HTTP/1.1\nCookie:\n\n";

            // act
            var (response, request) = await AnswerAsync(componentHub, content);

            // validation
            var setCookie = response.Headers.SetCookie.ToString();
            Assert.Contains($"session={request.Session.Id}", setCookie);
            Assert.Contains("Path=/", setCookie);
            Assert.Contains("HttpOnly", setCookie);

            // the lifetime is bounded, not the year-9999 "never expires" it used to carry
            Assert.Contains("Expires=", setCookie);
            Assert.DoesNotContain("9999", setCookie);

            // over plain http the cookie is not marked secure, or the browser would never send it back
            Assert.DoesNotContain("Secure", setCookie);
        }

        /// <summary>
        /// Behind a tls-terminating proxy the server sees plain http, so it cannot infer https
        /// from the request; the configuration forces the Secure flag on so the cookie is still
        /// marked https-only towards the browser.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_ConfigForcesSecure_MarksCookieSecure()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var content = $"GET {Endpoint} HTTP/1.1\nCookie:\n\n";
            var settings = new HttpServerSettings { Session = new SessionSettings { Secure = true } };

            // act
            var (response, _) = await AnswerAsync(componentHub, content, settings: settings);

            // validation
            Assert.Contains("Secure", response.Headers.SetCookie.ToString());
        }

        /// <summary>
        /// With expiry switched off the cookie must still be bounded - it becomes a session
        /// cookie with no Expires (dies when the browser closes) rather than one that never expires.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_TimeoutDisabled_IssuesSessionCookie()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            componentHub.SessionManager.Timeout = TimeSpan.Zero;
            var content = $"GET {Endpoint} HTTP/1.1\nCookie:\n\n";

            // act
            var (response, request) = await AnswerAsync(componentHub, content);

            // validation
            var setCookie = response.Headers.SetCookie.ToString();
            Assert.Contains($"session={request.Session.Id}", setCookie);
            Assert.DoesNotContain("Expires=", setCookie);
        }

        /// <summary>
        /// A cookie carrying an id the server never issued is answered with a fresh id, not
        /// with the one the client proposed - otherwise the client would choose the id of
        /// the session it is about to sign in to.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_UnknownSessionCookie_IssuesServerSideId()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var planted = Guid.NewGuid();
            var content = $"GET {Endpoint} HTTP/1.1\nCookie: session={planted}\n\n";

            // act
            var (response, request) = await AnswerAsync(componentHub, content);

            // validation
            var setCookie = response.Headers.SetCookie.ToString();
            Assert.NotEqual(planted, request.Session.Id);
            Assert.Contains($"session={request.Session.Id}", setCookie);
            Assert.DoesNotContain(planted.ToString(), setCookie);
        }

        /// <summary>
        /// A cookie naming a session the server issued is left alone: the client already
        /// holds the right id, and re-sending it on every response would be noise.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_KnownSessionCookie_SetsNoCookie()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var issued = componentHub.SessionManager.GetSession(UnitTestFixture.CreateRequestMock());
            var content = $"GET {Endpoint} HTTP/1.1\nCookie: session={issued.Id}\n\n";

            // act
            var (response, request) = await AnswerAsync(componentHub, content);

            // validation
            Assert.Same(issued, request.Session);
            Assert.True(StringValues.IsNullOrEmpty(response.Headers.SetCookie));
        }

        /// <summary>
        /// A client without a cookie signs in and gets back the id of the very session the
        /// identity was bound to. The request creates its session before any handler runs and
        /// the sign-in and the cookie must both refer to that one - were each to mint its own,
        /// the client would come back with an id that never signed in.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_SignInWithoutCookie_CookieNamesTheSignedInSession()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var identity = MockIdentityFactory.GetIdentity("Alice");
            var content = $"GET {Endpoint} HTTP/1.1\nCookie:\n\n";
            WebSession.Model.Session signedIn = null;

            // act
            var (response, request) = await AnswerAsync(componentHub, content, r =>
            {
                signedIn = componentHub.IdentityManager.Login(identity, r);
            });

            // validation
            Assert.NotNull(signedIn);
            Assert.Same(signedIn, request.Session);
            Assert.Contains($"session={signedIn.Id}", response.Headers.SetCookie.ToString());

            var next = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\nCookie: session={signedIn.Id}\n\n");
            Assert.Equal(identity, componentHub.IdentityManager.GetCurrentIdentity(next));
        }

        /// <summary>
        /// A request that never reaches a handler - here an unknown route - still hands the
        /// client its session id, since the login prompt and the redirect after a sign-in
        /// take the same path and must carry the (new) id.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_UnknownRoute_StillIssuesSessionCookie()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var content = $"GET /server/appa/no/such/route HTTP/1.1\nCookie: session={Guid.NewGuid()}\n\n";

            // act
            var (response, request) = await AnswerAsync(componentHub, content);

            // validation
            Assert.Equal(404, response.StatusCode);
            Assert.Contains($"session={request.Session.Id}", response.Headers.SetCookie.ToString());
        }
    }
}
