using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Unit tests for the protocol resolution of <see cref="HttpServer"/>, which offers HTTP/3
    /// by default wherever it can work and removes it wherever it cannot.
    /// </summary>
    public class UnitTestHttpServerProtocols
    {
        /// <summary>
        /// Tests the protocols applied for each combination of configuration, TLS and QUIC support.
        /// </summary>
        [Theory]
        // tls with quic: http/3 is offered unless the configuration says otherwise
        [InlineData(null, true, true, HttpProtocols.Http1AndHttp2AndHttp3)]
        [InlineData(HttpProtocols.Http1AndHttp2, true, true, HttpProtocols.Http1AndHttp2)]
        [InlineData(HttpProtocols.Http3, true, true, HttpProtocols.Http3)]
        // tls without quic: http/3 is removed, and a pure http/3 endpoint falls back to tcp
        [InlineData(null, true, false, HttpProtocols.Http1AndHttp2)]
        [InlineData(HttpProtocols.Http1AndHttp2AndHttp3, true, false, HttpProtocols.Http1AndHttp2)]
        [InlineData(HttpProtocols.Http3, true, false, HttpProtocols.Http1AndHttp2)]
        // plain http: quic needs tls, so the kestrel default stays and http/3 is removed
        [InlineData(null, false, true, null)]
        [InlineData(HttpProtocols.Http1, false, true, HttpProtocols.Http1)]
        [InlineData(HttpProtocols.Http1AndHttp2AndHttp3, false, true, HttpProtocols.Http1AndHttp2)]
        public void ResolveProtocols(HttpProtocols? configured, bool tls, bool quic, HttpProtocols? expected)
        {
            Assert.Equal(expected, HttpServer.ResolveProtocols(configured, tls, quic));
        }
    }
}
