using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Session;
using System.Net;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    public class UnitTestRequest
    {
        /// <summary>
        /// Prevents form actions from inheriting a container listener port absent from the public host.
        /// </summary>
        /// <param name="scheme">The HTTP scheme received by the server.</param>
        /// <param name="host">The public host header, including an optional explicit port.</param>
        /// <param name="externalUri">The independently configured sitemap base URI.</param>
        /// <param name="expected">The request URI that form rendering must receive.</param>
        [Theory]
        [InlineData("http", "vila", "http://vila/", "http://vila/vila/settings/mode?tab=1")]
        [InlineData("http", "vila", null, "http://vila/vila/settings/mode?tab=1")]
        [InlineData("http", "vila:80", "http://vila/", "http://vila/vila/settings/mode?tab=1")]
        [InlineData("http", "vila:9090", null, "http://vila:9090/vila/settings/mode?tab=1")]
        [InlineData("https", "vila", null, "https://vila/vila/settings/mode?tab=1")]
        [InlineData("https", "vila:8443", null, "https://vila:8443/vila/settings/mode?tab=1")]
        public void RequestUriPreservesPublicPort(string scheme, string host, string externalUri, string expected)
        {
            // arrange
            var features = Prepare(null, RequestMethod.GET, "?tab=1");
            features.Get<IHttpConnectionFeature>().LocalPort = 8080;
            var requestFeature = features.Get<IHttpRequestFeature>();
            requestFeature.Scheme = scheme;
            requestFeature.Headers.Host = host;
            requestFeature.RawTarget = "/vila/settings/mode?tab=1";
            var context = Fixture.UnitTestFixture.CreateHttpServerContextMock(externalUri: externalUri);

            // act
            var httpContext = new WebMessage.HttpContext(features, context);
            var request = httpContext.Request;

            // validation
            Assert.Equal(expected, request.Uri.ToString());
            Assert.Equal(expected, httpContext.Uri.ToString());
            Assert.Equal(8080, ((IPEndPoint)request.LocalEndPoint).Port);
        }

        /// <summary>
        /// The culture follows the first language the client accepts; a request without a usable
        /// language - curl, health checks, a wildcard or an unknown tag - gets the server culture.
        /// </summary>
        /// <param name="acceptLanguage">The Accept-Language header, or null when it is absent.</param>
        /// <param name="expected">The name of the expected culture.</param>
        [Theory]
        [InlineData("de,en;q=0.9,en-GB;q=0.8", "de")]
        [InlineData("en-GB, de;q=0.5", "en-GB")]
        [InlineData(null, "en")]
        [InlineData("", "en")]
        [InlineData("*", "en")]
        [InlineData("not a culture!", "en")]
        public void CultureFollowsAcceptLanguage(string acceptLanguage, string expected)
        {
            // arrange
            var features = PrepareGet();
            var headers = features.Get<IHttpRequestFeature>().Headers;
            headers.Remove("Accept-Language");
            if (acceptLanguage is not null)
            {
                headers.AcceptLanguage = acceptLanguage;
            }
            var context = Fixture.UnitTestFixture.CreateHttpServerContextMock();

            // act
            var request = new WebMessage.HttpContext(features, context).Request;

            // validation
            Assert.Equal(expected, request.Culture.Name);
        }

        /// <summary>
        /// The culture is resolved once per request and handed out read-only, so a caller
        /// cannot change it for the rest of the render.
        /// </summary>
        [Fact]
        public void CultureIsResolvedOncePerRequest()
        {
            // arrange
            var features = PrepareGet();
            var context = Fixture.UnitTestFixture.CreateHttpServerContextMock();
            var request = new WebMessage.HttpContext(features, context).Request;

            // act
            var first = request.Culture;
            var second = request.Culture;

            // validation
            Assert.Same(first, second);
            Assert.True(first.IsReadOnly);
        }

        /// <summary>
        /// Creates the feature collection of a plain get request to the root.
        /// </summary>
        /// <returns>The feature collection.</returns>
        private static IFeatureCollection PrepareGet()
        {
            var features = Prepare(null, RequestMethod.GET, "");
            var requestFeature = features.Get<IHttpRequestFeature>();
            requestFeature.Scheme = "http";
            requestFeature.RawTarget = "/";

            return features;
        }

        /// <summary>
        /// Creates the feature collection.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="method">The request method.</param>
        /// <param name="queryString">The query string.</param>
        /// <returns>The feature collection.</returns>
        protected static IFeatureCollection Prepare(BinaryReader reader, RequestMethod method, string queryString)
        {
            var contextFeatures = new FeatureCollection();
            contextFeatures.Set<IHttpConnectionFeature>(new HttpConnectionFeature()
            {
                LocalPort = 80,
                LocalIpAddress = IPAddress.Loopback,
                RemoteIpAddress = IPAddress.Loopback,
                ConnectionId = "43D885B6-DB4D-4EDF-9908-B122A5FFC829"
            });
            contextFeatures.Set<IHttpRequestFeature>(new HttpRequestFeature()
            {
                Path = "/",
                Protocol = "HTTP/1.1",
                Method = method.ToString(),
                QueryString = queryString,
                Headers = new HeaderDictionary()
            });
            contextFeatures.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature() { TraceIdentifier = "16FC666F-D7DC-47FF-9FB8-9D0F8DFCEF99" });
            contextFeatures.Set<ISessionFeature>(new SessionFeature() { });

            var requestFeature = contextFeatures.Get<IHttpRequestFeature>();
            requestFeature.Headers.Host = "localhost";
            requestFeature.Headers.Connection = "keep-alive";
            requestFeature.Headers.ContentType = "";
            requestFeature.Headers.ContentLength = 0;
            requestFeature.Headers.ContentLanguage = "";
            requestFeature.Headers.ContentEncoding = "utf-8";
            requestFeature.Headers.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
            requestFeature.Headers.AcceptEncoding = "gzip, deflate, br";
            requestFeature.Headers.AcceptLanguage = "de,en;q=0.9,en-GB;q=0.8,de-DE;q=0.7,en-US;q=0.6";
            requestFeature.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.80 Safari/537.36 Edg/98.0.1108.50";

            //requestFeature.Headers.Cookie;

            requestFeature.Headers.Authorization = "";

            return contextFeatures;
        }
    }
}
