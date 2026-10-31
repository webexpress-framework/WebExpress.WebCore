using System.Net;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Behind a reverse proxy only X-Forwarded-For names the client, and any caller can send that
    /// header. These tests pin down that it is believed exactly as far as trusted proxies vouch for it.
    /// The fixture's requests arrive from 127.0.0.1, which plays the proxy.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestForwardedClientResolver
    {
        /// <summary>
        /// Resolves the client of a request carrying the given forwarding chain.
        /// </summary>
        /// <param name="forwardedFor">The X-Forwarded-For value, or null to send none.</param>
        /// <param name="trustedProxies">The configured proxies.</param>
        /// <returns>The resolved client address as text.</returns>
        private static string Resolve(string forwardedFor, params string[] trustedProxies)
        {
            var header = forwardedFor is null ? string.Empty : $"X-Forwarded-For: {forwardedFor}\n";
            var request = UnitTestFixture.CreateRequestMock($"GET / HTTP/1.1\n{header}\n");
            var resolver = new ForwardedClientResolver(new SecuritySettings { TrustedProxies = [.. trustedProxies] });

            return resolver.Resolve(request)?.ToString();
        }

        /// <summary>
        /// Without configured proxies the header is never believed, whatever it claims.
        /// </summary>
        [Fact]
        public void UntrustedConnectionIgnoresTheHeader()
        {
            Assert.Equal("127.0.0.1", Resolve("203.0.113.7"));
            Assert.Equal("127.0.0.1", Resolve("203.0.113.7", "10.0.0.1"));
        }

        /// <summary>
        /// A connection from a trusted proxy - named singly or by range - is attributed to the
        /// client the proxy reports.
        /// </summary>
        /// <param name="proxy">The configured proxy.</param>
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("127.0.0.0/8")]
        [InlineData(" 127.0.0.1 ")]
        public void TrustedProxyNamesTheClient(string proxy)
        {
            Assert.Equal("203.0.113.7", Resolve("203.0.113.7", proxy));
        }

        /// <summary>
        /// Through several proxies the client is the last address no trusted proxy accounts for;
        /// what the client wrote further left cannot choose the address it is limited under.
        /// </summary>
        [Fact]
        public void ChainIsReadFromTheTrustedEnd()
        {
            Assert.Equal("203.0.113.7", Resolve("198.51.100.1, 203.0.113.7, 10.0.0.2", "127.0.0.1", "10.0.0.0/8"));
        }

        /// <summary>
        /// A chain that names no one reliably leaves the request with the proxy, which keeps it limited.
        /// </summary>
        /// <param name="forwardedFor">The X-Forwarded-For value.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-an-address")]
        [InlineData("203.0.113.7, garbage")]
        [InlineData("10.0.0.2")]
        public void UnusableChainFallsBackToTheConnection(string forwardedFor)
        {
            Assert.Equal("127.0.0.1", Resolve(forwardedFor, "127.0.0.1", "10.0.0.0/8"));
        }

        /// <summary>
        /// Entries that are neither an address nor a range are skipped instead of failing the start;
        /// a range written with host bits names the network it lies in.
        /// </summary>
        [Fact]
        public void InvalidEntriesAreIgnored()
        {
            var resolver = new ForwardedClientResolver(new SecuritySettings
            {
                TrustedProxies = ["proxy.example", "10.0.0.5/8", null, "", "192.168.0.0/16", "::1"]
            });

            Assert.Equal(
                [IPNetwork.Parse("10.0.0.0/8"), IPNetwork.Parse("192.168.0.0/16"), IPNetwork.Parse("::1/128")],
                resolver.TrustedProxies);
        }
    }
}
