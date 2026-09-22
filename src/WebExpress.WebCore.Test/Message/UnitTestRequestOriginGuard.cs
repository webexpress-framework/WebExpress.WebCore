using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for <see cref="RequestOriginGuard"/>, which rejects requests another site
    /// triggers in the browser of a signed-in user.
    /// </summary>
    public class UnitTestRequestOriginGuard
    {
        /// <summary>
        /// Builds a request with the given method and extra header lines.
        /// </summary>
        /// <param name="method">The request method.</param>
        /// <param name="headers">Header lines in wire format.</param>
        /// <returns>The request.</returns>
        private static IRequest Request(string method, params string[] headers)
        {
            return UnitTestFixture.CreateRequestMock($"{method} /app HTTP/1.1\r\n{string.Join("\r\n", headers)}\r\n\r\n");
        }

        /// <summary>
        /// Tests the decision for typical browser and non-browser requests.
        /// </summary>
        [Theory]
        [InlineData("GET", true, "Origin: https://evil.example", "Sec-Fetch-Site: cross-site")]
        [InlineData("POST", true, "Origin: http://localhost", "Sec-Fetch-Site: same-origin")]
        [InlineData("POST", true, "Origin: http://localhost")]
        [InlineData("POST", true, "Sec-Fetch-Site: none")]
        [InlineData("POST", true)]
        [InlineData("POST", false, "Origin: https://evil.example", "Sec-Fetch-Site: cross-site")]
        [InlineData("DELETE", false, "Origin: https://evil.example")]
        [InlineData("PUT", false, "Origin: null", "Sec-Fetch-Site: cross-site")]
        [InlineData("POST", false, "Origin: https://sub.localhost", "Sec-Fetch-Site: same-site")]
        [InlineData("POST", true, "Origin: https://public.example", "Sec-Fetch-Site: same-origin")]
        [InlineData("POST", true, "Origin: https://evil.example", "Authorization: Bearer abc")]
        public void Decision(string method, bool allowed, params string[] headers)
        {
            var guard = new RequestOriginGuard(null);

            Assert.Equal(allowed, guard.IsAllowed(Request(method, headers)));
        }

        /// <summary>
        /// Tests that a websocket is checked although it opens with a GET, since browsers
        /// connect it across sites and send the cookies along.
        /// </summary>
        [Fact]
        public void CrossSiteWebSocketIsRejected()
        {
            var guard = new RequestOriginGuard(null);
            var request = Request("GET", "Origin: https://evil.example", "Sec-Fetch-Site: cross-site");

            Assert.False(guard.IsAllowed(request, webSocket: true));
        }

        /// <summary>
        /// Tests that a configured origin is accepted and that the check can be switched off.
        /// </summary>
        [Fact]
        public void SettingsTrustOriginsAndDisableTheCheck()
        {
            var request = Request("POST", "Origin: https://portal.example", "Sec-Fetch-Site: cross-site");

            Assert.True(new RequestOriginGuard(new SecuritySettings { TrustedOrigins = ["https://portal.example/"] }).IsAllowed(request));
            Assert.True(new RequestOriginGuard(new SecuritySettings { CsrfProtection = false }).IsAllowed(request));
            Assert.False(new RequestOriginGuard(new SecuritySettings()).IsAllowed(request));
        }
    }
}
