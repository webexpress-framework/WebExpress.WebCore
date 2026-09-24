using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSitemap;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Tests the answer to a refused request (<c>HttpServer.CreateAccessDeniedResponse</c>), which
    /// a policy refusal and a <see cref="ForbiddenException"/> share: in place, never a redirect.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHttpServerAccessDenied
    {
        /// <summary>
        /// A REST endpoint has no page to show: a signed-in caller is answered 403, a caller who
        /// is not signed in 401.
        /// </summary>
        [Fact]
        public void AnEndpointWithoutAPageIsAnsweredWithTheBareStatus()
        {
            UnitTestFixture.CreateAndRegisterComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock();
            var identity = new MockIdentity(Guid.NewGuid(), "caller", "caller@example.com", "x");

            Assert.IsType<ResponseForbidden>(HttpServer.CreateAccessDeniedResponse(request, new SearchResult(), identity));
            Assert.IsType<ResponseUnauthorized>(HttpServer.CreateAccessDeniedResponse(request, new SearchResult(), null));
        }

        /// <summary>
        /// A page is refused where it was asked for: whatever the answer - the forbidden page,
        /// the sign-in prompt or the status page standing in for either - it is not a redirect.
        /// </summary>
        [Fact]
        public void APageIsRefusedInPlace()
        {
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var request = UnitTestFixture.CreateRequestMock();
            var page = componentHub.PageManager.Pages.First();
            var searchResult = new SearchResult { EndpointContext = page };
            var identity = new MockIdentity(Guid.NewGuid(), "caller", "caller@example.com", "x");

            foreach (var caller in new[] { identity, null })
            {
                var response = HttpServer.CreateAccessDeniedResponse(request, searchResult, caller);

                Assert.NotNull(response);
                Assert.IsNotType<ResponseMovedTemporarily>(response);
                Assert.IsNotType<ResponseMovedPermanently>(response);
                Assert.IsNotType<ResponseOK>(response);
            }
        }

        /// <summary>
        /// The exception names nothing the caller sees; its message is for the log.
        /// </summary>
        [Fact]
        public void TheExceptionCarriesItsReasonForTheLog()
        {
            Assert.False(string.IsNullOrWhiteSpace(new ForbiddenException().Message));
            Assert.Equal("no grant on SD", new ForbiddenException("no grant on SD").Message);
        }
    }
}
