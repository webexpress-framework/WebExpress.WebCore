using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the host-header handling of <see cref="HttpContext"/>.
    /// </summary>
    /// <remarks>
    /// A client addressing a non-default port sends it in the host header, while the port of
    /// the connection is known separately. Taking the header as the host name would produce a
    /// second port and an unparsable uri, which fails before a request context exists - so
    /// every request on such a port answers an empty error naming nothing. The tests pin the
    /// forms a host header actually arrives in.
    /// </remarks>
    public class UnitTestHttpContextHost
    {
        /// <summary>
        /// Tests that a host header keeps only its name, whatever port it carries.
        /// </summary>
        /// <param name="host">The value of the host header.</param>
        /// <param name="expected">The expected host name.</param>
        [Theory]
        [InlineData("localhost", "localhost")]
        [InlineData("localhost:8080", "localhost")]
        [InlineData("example.org:443", "example.org")]
        [InlineData("192.168.0.5:5000", "192.168.0.5")]
        [InlineData("[::1]", "[::1]")]
        [InlineData("[::1]:8080", "[::1]")]
        [InlineData("[2001:db8::1]:80", "[2001:db8::1]")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void HostNameOf(string host, string expected)
        {
            // act
            var name = HttpContext.HostNameOf(host);

            // validation
            Assert.Equal(expected, name);
        }

        /// <summary>
        /// Tests that the host name is usable as the host of a uri together with a port taken
        /// from elsewhere - the case the production code builds.
        /// </summary>
        /// <param name="host">The value of the host header.</param>
        /// <param name="port">The port of the connection.</param>
        /// <param name="expected">The expected uri.</param>
        [Theory]
        [InlineData("localhost:8080", 8080, "http://localhost:8080/")]
        [InlineData("localhost", 80, "http://localhost/")]
        [InlineData("[::1]:8080", 8080, "http://[::1]:8080/")]
        public void HostNameBuildsUri(string host, int port, string expected)
        {
            // act
            var uri = new UriBuilder("http", HttpContext.HostNameOf(host), port).Uri;

            // validation
            Assert.Equal(expected, uri.ToString());
        }

        /// <summary>
        /// Tests that the request target never replaces the authority of the request: a target
        /// starting with "//" is a path, not a network-path reference, so the routing cannot be
        /// steered to "/admin" while a proxy in front saw "//evil.com/admin".
        /// </summary>
        /// <param name="target">The request target as the client sent it.</param>
        /// <param name="expected">The expected uri.</param>
        [Theory]
        [InlineData("/a/b?c=1", "http://localhost:8080/a/b?c=1")]
        [InlineData("//evil.com/admin", "http://localhost:8080//evil.com/admin")]
        [InlineData("http://other.org/x?y=2", "http://localhost:8080/x?y=2")]
        [InlineData("*", "http://localhost:8080/")]
        [InlineData("", "http://localhost:8080/")]
        [InlineData(null, "http://localhost:8080/")]
        public void ResolveTargetKeepsTheAuthority(string target, string expected)
        {
            // act
            var uri = HttpContext.ResolveTarget(new Uri("http://localhost:8080/"), target);

            // validation
            Assert.Equal("localhost", uri.Host);
            Assert.Equal(8080, uri.Port);
            Assert.Equal(expected, uri.ToString());
        }

        /// <summary>
        /// Tests that a compressed body does not fail the context: Content-Encoding names a
        /// compression such as gzip, not a character set, and reading it as one threw before the
        /// request existed, so every compressed post was answered with a 500.
        /// </summary>
        /// <param name="contentEncoding">The value of the Content-Encoding header.</param>
        [Theory]
        [InlineData("gzip")]
        [InlineData("br")]
        [InlineData("deflate, gzip")]
        public void ContentEncodingIsNoCharset(string contentEncoding)
        {
            // act
            var context = Fixture.UnitTestFixture.CreateHttpContextMock($"POST /a HTTP/1.1\r\nContent-Encoding: {contentEncoding}\r\n\r\n");

            // validation
            Assert.Equal(contentEncoding, context.Request.Header.ContentEncoding);
            Assert.Equal("utf-8", context.Encoding.WebName);
        }
    }
}
