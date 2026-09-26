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
        /// Preserves the configured public URI when the server creates its shared context.
        /// </summary>
        [Fact]
        public void ExternalUriIsAvailableFromServerContext()
        {
            // arrange
            const string externalUri = "https://www.example.com/";
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock(externalUri: externalUri));

            // validation
            Assert.Equal(externalUri, server.HttpServerContext.ExternalUri);
        }

        /// <summary>
        /// Prevents a missing HTTPS certificate from silently registering an unencrypted listener.
        /// </summary>
        /// <param name="scheme">The configured HTTPS spelling normalized by the URI parser.</param>
        [Theory]
        [InlineData("https")]
        [InlineData("HTTPS")]
        public void HttpsWithoutCertificateNeverRegistersPlainHttpListener(string scheme)
        {
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock());
            var options = new Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions();
            var wrapper = new Microsoft.Extensions.Options.OptionsWrapper<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options);
            var endpoint = new EndpointSettings
            {
                Uri = $"{scheme}://localhost:5001/",
                PfxFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pfx")
            };
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var addEndpoint = typeof(HttpServer).GetMethod("AddEndpoint", flags, null,
                [wrapper.GetType(), typeof(EndpointSettings), typeof(Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols?)], null);

            Assert.Throws<System.Reflection.TargetInvocationException>(() => addEndpoint.Invoke(server, [wrapper, endpoint, null]));

            var listeners = typeof(Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions)
                .GetProperty("CodeBackedListenOptions", flags).GetValue(options);
            Assert.Empty((System.Collections.IEnumerable)listeners);
        }

        /// <summary>
        /// An endpoint another process already holds - a second instance, say - ends the start
        /// with a result instead of an exception, so the host can log it and exit cleanly.
        /// </summary>
        [Fact]
        public void Start_EndpointInUse_ReturnsFalseInsteadOfThrowing()
        {
            // arrange
            using var occupant = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            occupant.Start();
            var port = ((System.Net.IPEndPoint)occupant.LocalEndpoint).Port;
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings { Endpoints = [new() { Uri = $"http://127.0.0.1:{port}" }] }
            };

            var errors = server.HttpServerContext.Log.ErrorCount;

            try
            {
                // act
                var started = true;
                var exception = Record.Exception(() => started = server.Start());

                // validation
                Assert.Null(exception);
                Assert.False(started);
                Assert.Equal(errors + 1, server.HttpServerContext.Log.ErrorCount);
            }
            finally
            {
                server.Stop();
            }
        }

        /// <summary>
        /// Preserves normal and missing-route responses when startup constructs the server before its component hub.
        /// </summary>
        /// <param name="path">The application route whose response must survive the production startup order.</param>
        /// <param name="status">The expected response status after the component hub becomes available.</param>
        /// <returns>A task that completes after the HTTP response has been verified.</returns>
        [Theory]
        [InlineData(Endpoint, 400)]
        [InlineData("/server/appa/no/such/route", 404)]
        public async Task ProcessRequestAsync_ServerCreatedBeforeHub_PreservesResponse(string path, int status)
        {
            var hubField = typeof(WebEx).GetField("_componentHub", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var previousHub = WebEx.ComponentHub;
            WebComponent.ComponentHub componentHub = null;
            try
            {
                hubField.SetValue(null, null);
                var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock());
                componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
                componentHub.SitemapManager.Refresh();
                var context = UnitTestFixture.CreateHttpContextMock($"GET {path} HTTP/1.1\r\nCookie:\r\n\r\n");
                var response = new HttpResponseFeature();
                using var output = new MemoryStream();
                context.Features.Set<IHttpResponseFeature>(response);
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));

                await server.ProcessRequestAsync(context);

                Assert.Equal(status, response.StatusCode);
                Assert.Null(((WebMessage.RequestBase)context.Request).ExistingSession);
                Assert.DoesNotContain("session=", response.Headers.SetCookie.ToString());
            }
            finally
            {
                componentHub?.IdentityProviderManager.Dispose();
                hubField.SetValue(null, previousHub);
            }
        }

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
            var (response, request) = await AnswerAsync(componentHub, content, r => _ = r.Session);

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
            var (response, _) = await AnswerAsync(componentHub, content, r => _ = r.Session, settings: settings);

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
            var (response, request) = await AnswerAsync(componentHub, content, r => _ = r.Session);

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
            var (response, request) = await AnswerAsync(componentHub, content, r => _ = r.Session);

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
            var (response, request) = await AnswerAsync(componentHub, content, r => _ = r.Session);

            // validation
            Assert.Same(issued, request.Session);
            Assert.True(StringValues.IsNullOrEmpty(response.Headers.SetCookie));
        }

        /// <summary>
        /// Token cookies reach the wire even when login completes before a handler returns its response.
        /// </summary>
        [Fact]
        public async Task ProcessRequestAsync_SignInIssuesTokenCookiesWithoutSession()
        {
            using var fixture = new AuthenticationFixture();
            var pair = default(WebIdentity.IdentityTokenPair);
            var (response, request) = await AnswerAsync(fixture.Hub, $"GET {Endpoint} HTTP/1.1\nCookie:\n\n", r =>
            {
                ((WebMessage.RequestBase)r).ApplicationContext = fixture.Application;
                pair = fixture.Manager.Login(MockIdentityFactory.GetIdentity("Alice"), r);
            });
            var cookies = response.Headers.SetCookie.ToString();
            Assert.Contains($"{WebIdentity.IdentityManager.AccessCookieName}={pair.AccessToken}", cookies);
            Assert.Contains($"{WebIdentity.IdentityManager.RefreshCookieName}={pair.RefreshToken}", cookies);
            Assert.Contains("SameSite=Lax", cookies);
            Assert.Contains("Secure", cookies);
            Assert.Contains("HttpOnly", cookies);
            Assert.DoesNotContain("session=", cookies);
            Assert.Null(((WebMessage.RequestBase)request).ExistingSession);
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
            var (response, request) = await AnswerAsync(componentHub, content, r => _ = r.Session);

            // validation
            Assert.Equal(404, response.StatusCode);
            Assert.Contains($"session={request.Session.Id}", response.Headers.SetCookie.ToString());
        }

        /// <summary>
        /// A protected page is served only to an identity holding its policy. An anonymous
        /// request is refused even when no provider of the application can offer a login,
        /// rather than the page being served as if it were public.
        /// </summary>
        /// <param name="authenticated">Whether a real signed login supplies the request identity.</param>
        /// <param name="grantPolicy">Whether the login grants the page's policy.</param>
        /// <param name="status">The expected response status.</param>
        /// <returns>A task that completes after the HTTP response has been verified.</returns>
        [Theory]
        [InlineData(false, false, 401)]
        [InlineData(true, false, 403)]
        [InlineData(true, true, 200)]
        public async Task ProcessRequestAsync_ProtectedPage_RequiresPolicy(bool authenticated, bool grantPolicy, int status)
        {
            // arrange
            using var fixture = new AuthenticationFixture();
            var page = (WebPage.PageContext)fixture.Hub.PageManager.GetPages<WWW.About>(fixture.Application).Single();
            page.Policies = [new TestIdentityPolicyA()];

            // act
            var (response, _) = await AnswerAsync(fixture.Hub, "GET /server/appa/about HTTP/1.1\nCookie:\n\n", r =>
            {
                if (authenticated)
                {
                    ((WebMessage.RequestBase)r).ApplicationContext = fixture.Application;
                    fixture.Manager.Login(new WebIdentity.Identity(Guid.NewGuid(), "test-user",
                        policyNames: grantPolicy ? [typeof(TestIdentityPolicyA).FullName] : []), r);
                }
            });

            // validation
            Assert.Equal(status, response.StatusCode);
        }
    }
}
