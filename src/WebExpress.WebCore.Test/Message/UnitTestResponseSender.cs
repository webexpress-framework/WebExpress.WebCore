using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebHtml.Parser;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for <see cref="ResponseSender"/>, focusing on the handling of connection-specific
    /// headers across HTTP protocol versions where their validity differs.
    /// </summary>
    public class UnitTestResponseSender
    {
        /// <summary>
        /// Sends the given response through the sender on a connection negotiated with the specified
        /// protocol and returns the populated response feature for header inspection.
        /// </summary>
        /// <param name="protocol">The negotiated protocol, e.g. "HTTP/1.1" or "HTTP/2".</param>
        /// <param name="response">The response to send.</param>
        /// <returns>The response feature carrying the emitted headers and status.</returns>
        private static async Task<IHttpResponseFeature> Send(string protocol, IResponse response)
        {
            return (await Send(protocol, "http", response, null)).Feature;
        }

        /// <summary>
        /// Sends the given response over the given scheme with the given security settings and
        /// returns the emitted headers together with the written body.
        /// </summary>
        /// <param name="protocol">The negotiated protocol, e.g. "HTTP/1.1" or "HTTP/2".</param>
        /// <param name="scheme">The request scheme, "http" or "https".</param>
        /// <param name="response">The response to send.</param>
        /// <param name="settings">The security settings, or null for the built-in defaults.</param>
        /// <returns>The response feature and the body as text.</returns>
        private static async Task<(IHttpResponseFeature Feature, string Body)> Send(string protocol, string scheme, IResponse response, SecuritySettings settings)
        {
            var features = new FeatureCollection();
            var body = new FakeResponseBodyFeature();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature { Protocol = protocol, Scheme = scheme, Headers = new HeaderDictionary() });
            features.Set<IHttpResponseFeature>(new HttpResponseFeature { Headers = new HeaderDictionary() });
            features.Set<IHttpResponseBodyFeature>(body);

            await new ResponseSender(new SecurityHeaders(settings)).SendAsync(new FakeHttpContext(features), response);

            return (features.Get<IHttpResponseFeature>(), Encoding.UTF8.GetString(((MemoryStream)body.Stream).ToArray()));
        }

        /// <summary>
        /// Tests that a server without any security configuration still sends the protective
        /// headers, and that HSTS stays off over plain http where browsers ignore it.
        /// </summary>
        [Fact]
        public async Task SecurityHeadersAreSentByDefault()
        {
            var (feature, _) = await Send("HTTP/1.1", "http", new ResponseOK(), null);

            Assert.Equal("nosniff", feature.Headers["X-Content-Type-Options"]);
            Assert.Equal("SAMEORIGIN", feature.Headers["X-Frame-Options"]);
            Assert.Equal("strict-origin-when-cross-origin", feature.Headers["Referrer-Policy"]);
            Assert.Contains("object-src 'none'", feature.Headers["Content-Security-Policy"].ToString());
            Assert.False(feature.Headers.ContainsKey("Strict-Transport-Security"));
        }

        /// <summary>
        /// Tests that HSTS is announced on https responses.
        /// </summary>
        [Fact]
        public async Task HstsIsSentOverHttps()
        {
            var (feature, _) = await Send("HTTP/2", "https", new ResponseOK(), null);

            Assert.Equal("max-age=31536000", feature.Headers["Strict-Transport-Security"]);
        }

        /// <summary>
        /// Tests that the inline scripts the server renders carry the very nonce the policy of
        /// the same response allows, so they keep running under the strict policy.
        /// </summary>
        [Fact]
        public async Task InlineScriptCarriesNonceOfPolicy()
        {
            var response = new ResponseOK() { Content = new HtmlElementScriptingScript("let a = 1;") };

            var (feature, body) = await Send("HTTP/1.1", "http", response, null);

            var nonce = Regex.Match(feature.Headers["Content-Security-Policy"].ToString(), "'nonce-([^']+)'").Groups[1].Value;
            Assert.NotEmpty(nonce);
            Assert.Contains($"nonce=\"{nonce}\"", body);
        }

        /// <summary>
        /// Tests that an html response without an explicit type is still declared as html, since
        /// nosniff forbids the browser to guess and it would otherwise display the markup as text.
        /// </summary>
        [Fact]
        public async Task HtmlContentIsTypedAsHtml()
        {
            var response = new ResponseOK() { Content = new HtmlElementTextContentDiv() };

            var (feature, _) = await Send("HTTP/1.1", "http", response, null);

            Assert.Equal("text/html; charset=utf-8", feature.Headers.ContentType);
        }

        /// <summary>
        /// Tests that a script read from markup, which may stem from user input, gets no nonce
        /// and is therefore still blocked by the browser.
        /// </summary>
        [Fact]
        public async Task ParsedScriptGetsNoNonce()
        {
            var parsed = new HtmlParser().Parse("<div><script>alert(1)</script></div>");
            var response = new ResponseOK() { Content = parsed.First() };

            var (_, body) = await Send("HTTP/1.1", "http", response, null);

            Assert.Contains("<script", body);
            Assert.DoesNotContain("nonce=", body);
        }

        /// <summary>
        /// Tests that a header set by the handler is sent and wins over the server-wide default.
        /// </summary>
        [Fact]
        public async Task CustomHeaderOverridesDefault()
        {
            var response = new ResponseOK();
            response.Header.CustomHeader["Referrer-Policy"] = "no-referrer";
            response.Header.CustomHeader["ETag"] = "\"abc\"";

            var (feature, _) = await Send("HTTP/1.1", "http", response, null);

            Assert.Equal("no-referrer", feature.Headers["Referrer-Policy"]);
            Assert.Equal("\"abc\"", feature.Headers.ETag);
        }

        /// <summary>
        /// Tests that the settings can switch individual headers off, replace them, and move the
        /// policy into report-only mode.
        /// </summary>
        [Fact]
        public async Task SettingsOverrideDefaults()
        {
            var settings = new SecuritySettings
            {
                ContentSecurityPolicy = "default-src 'self' 'nonce-{nonce}'",
                ContentSecurityPolicyReportOnly = true,
                HstsMaxAge = 0,
                Headers = new() { ["X-Frame-Options"] = "", ["Referrer-Policy"] = "no-referrer" }
            };

            var (feature, _) = await Send("HTTP/1.1", "https", new ResponseOK(), settings);

            Assert.False(feature.Headers.ContainsKey("Content-Security-Policy"));
            Assert.StartsWith("default-src 'self' 'nonce-", feature.Headers["Content-Security-Policy-Report-Only"].ToString());
            Assert.False(feature.Headers.ContainsKey("Strict-Transport-Security"));
            Assert.False(feature.Headers.ContainsKey("X-Frame-Options"));
            Assert.Equal("no-referrer", feature.Headers["Referrer-Policy"]);
        }

        /// <summary>
        /// Tests that cookies take the configured SameSite default unless they state their own.
        /// </summary>
        [Fact]
        public async Task CookieSameSiteFollowsSettingsAndPerCookieOverride()
        {
            var response = new ResponseOK();
            response.Header.Cookies.Add(new Cookie("a", "1"));
            response.Header.Cookies.Add(new Cookie("b", "2"));
            response.Header.CookieSameSite["b"] = SameSiteMode.Lax;

            var (feature, _) = await Send("HTTP/1.1", "http", response, new SecuritySettings { CookieSameSite = SameSiteMode.Strict });

            var cookies = feature.Headers.SetCookie.ToArray();
            Assert.Contains(cookies, x => x.StartsWith("a=1") && x.EndsWith("SameSite=Strict"));
            Assert.Contains(cookies, x => x.StartsWith("b=2") && x.EndsWith("SameSite=Lax"));
        }

        /// <summary>
        /// Tests that the connection-specific Keep-Alive header is never emitted: persistence is
        /// owned by Kestrel and the header is forbidden on HTTP/2.
        /// </summary>
        [Theory]
        [InlineData("HTTP/1.1")]
        [InlineData("HTTP/2")]
        public async Task KeepAliveHeaderIsNeverSent(string protocol)
        {
            var feature = await Send(protocol, new ResponseOK());

            Assert.False(feature.Headers.ContainsKey("Keep-Alive"));
        }

        /// <summary>
        /// Tests that the websocket handshake headers are emitted on HTTP/1.x, where they are valid.
        /// </summary>
        [Fact]
        public async Task ConnectionAndUpgradeEmittedOnHttp1()
        {
            var response = new ResponseSwitchingProtocols("upgrade", "websocket", "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=");

            var feature = await Send("HTTP/1.1", response);

            Assert.True(feature.Headers.ContainsKey("Connection"));
            Assert.True(feature.Headers.ContainsKey("Upgrade"));
        }

        /// <summary>
        /// Tests that the connection-specific Connection and Upgrade headers are suppressed on
        /// HTTP/2, where Kestrel rejects them (RFC 9113 §8.2.2).
        /// </summary>
        [Fact]
        public async Task ConnectionAndUpgradeSuppressedOnHttp2()
        {
            var response = new ResponseSwitchingProtocols("upgrade", "websocket", "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=");

            var feature = await Send("HTTP/2", response);

            Assert.False(feature.Headers.ContainsKey("Connection"));
            Assert.False(feature.Headers.ContainsKey("Upgrade"));
        }

        /// <summary>
        /// A minimal <see cref="IHttpContext"/> backed by an explicit feature collection, used to
        /// drive the sender without constructing a full request pipeline.
        /// </summary>
        private sealed class FakeHttpContext : IHttpContext
        {
            public FakeHttpContext(IFeatureCollection features) => Features = features;

            public IHttpServerContext HttpServerContext => null;
            public string Id => "test";
            public IRequest Request => null;
            public EndPoint LocalEndPoint => new IPEndPoint(IPAddress.Loopback, 80);
            public EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 12345);
            public IFeatureCollection Features { get; }
            public Encoding Encoding => Encoding.UTF8;
            public Uri Uri => new("http://localhost/");
        }

        /// <summary>
        /// A minimal response body feature that discards the written body into an in-memory stream.
        /// </summary>
        private sealed class FakeResponseBodyFeature : IHttpResponseBodyFeature
        {
            public Stream Stream { get; } = new MemoryStream();
            public PipeWriter Writer => PipeWriter.Create(Stream);
            public Task CompleteAsync() => Task.CompletedTask;
            public void DisableBuffering() { }
            public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
