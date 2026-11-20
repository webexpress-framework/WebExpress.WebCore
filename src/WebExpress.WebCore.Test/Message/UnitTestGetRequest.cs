using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// UnitTestGetRequest class for testing HTTP GET requests.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestGetRequest
    {
        /// <summary>
        /// Tests a general GET request.
        /// </summary>
        [Fact]
        public void General()
        {
            var content = UnitTestFixture.GetEmbeddedResource("general.get");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("http://localhost/abc/xyz/A7BCCCA9-4C7E-4117-9EE2-ECC3381B605A", request.Uri?.ToString());
        }

        /// <summary>
        /// Tests a GET request with less data.
        /// </summary>
        [Fact]
        public void Less()
        {
            var content = UnitTestFixture.GetEmbeddedResource("less.get");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("http://localhost/abc/xyz/A7BCCCA9-4C7E-4117-9EE2-ECC3381B605A", request.Uri?.ToString());
        }

        /// <summary>
        /// Tests a GET request with massive data.
        /// </summary>
        [Fact]
        public void Massive()
        {
            var content = UnitTestFixture.GetEmbeddedResource("massive.get");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("http://localhost/abc/xyz/A7BCCCA9-4C7E-4117-9EE2-ECC3381B605A", request.Uri?.ToString());
        }

        /// <summary>
        /// Tests a GET request with parameters.
        /// </summary>
        [Fact]
        public void GetParameter()
        {
            var content = UnitTestFixture.GetEmbeddedResource("param.get");
            var request = UnitTestFixture.CreateRequestMock(content);
            var param = request?.GetParameter("a")?.Value;

            Assert.Equal("http://localhost/abc/xyz/A7BCCCA9-4C7E-4117-9EE2-ECC3381B605A", request.Uri?.ToString());
            Assert.Equal("1", param);
        }

        /// <summary>
        /// Tests a GET request with parameters containing umlauts.
        /// </summary>
        [Fact]
        public void GetParameterWithUmlaut()
        {
            var content = UnitTestFixture.GetEmbeddedResource("param_umlaut.get");
            var request = UnitTestFixture.CreateRequestMock(content);
            var a = request?.GetParameter("a")?.Value;
            var b = request?.GetParameter("b")?.Value;

            Assert.Equal("http://localhost/abc/xyz/A7BCCCA9-4C7E-4117-9EE2-ECC3381B605A", request.Uri?.ToString());
            Assert.Equal("ä", a);
            Assert.Equal("ö ü", b);
        }

        /// <summary>
        /// Tests that a query is decoded: a plus is a blank, an escape its character.
        /// </summary>
        [Fact]
        public void GetParameterDecodesItsEscapes()
        {
            var request = UnitTestFixture.CreateRequestMock("GET /abc?a=C%2B%2B&b=a+b HTTP/1.1\r\nHost: localhost\r\n\r\n");

            Assert.Equal("C++", request?.GetParameter("a")?.Value);
            Assert.Equal("a b", request?.GetParameter("b")?.Value);
        }

        /// <summary>
        /// Tests that the keys of a query are decoded as their values are, so a field named
        /// items[] is found by that name; a value may carry '=' itself.
        /// </summary>
        [Fact]
        public void GetParameterDecodesItsKeys()
        {
            var request = UnitTestFixture.CreateRequestMock("GET /abc?items%5B%5D=a&first+name=b&c=x%3Dy=z&d HTTP/1.1\r\nHost: localhost\r\n\r\n");

            Assert.Equal("a", request?.GetParameter("items[]")?.Value);
            Assert.Equal("b", request?.GetParameter("first name")?.Value);
            Assert.Equal("x=y=z", request?.GetParameter("c")?.Value);
            Assert.True(request?.HasParameter("d"));
        }

        /// <summary>
        /// Tests that a parameter created in code keeps its plain value and is encoded only
        /// when it is written into an address.
        /// </summary>
        [Fact]
        public void ParameterKeepsItsPlainValue()
        {
            var parameter = new Parameter("a", "C++ 100%", ParameterScope.Parameter);

            Assert.Equal("C++ 100%", parameter.Value);
            Assert.Equal("a=C%2B%2B%20100%25", parameter.ToString());
        }

        /// <summary>
        /// Tests that a key repeated in the query always resolves to its last occurrence. The
        /// query was parsed in parallel once, so which value won changed from request to request.
        /// </summary>
        [Fact]
        public void RepeatedQueryKeyTakesTheLastValue()
        {
            var query = string.Join("&", Enumerable.Range(0, 200).Select(x => $"page={x}"));

            for (var i = 0; i < 20; i++)
            {
                var request = UnitTestFixture.CreateRequestMock($"GET /a?{query} HTTP/1.1\r\n\r\n");

                Assert.Equal("199", request.GetParameter("page")?.Value);
            }
        }

        /// <summary>
        /// Tests that a method the framework does not implement is not taken for a GET, which
        /// would answer an OPTIONS preflight or a PROPFIND with the resource itself.
        /// </summary>
        /// <param name="method">The request method.</param>
        [Theory]
        [InlineData("OPTIONS")]
        [InlineData("PROPFIND")]
        [InlineData("CONNECT")]
        [InlineData("TRACE")]
        public void UnknownMethodIsNoGet(string method)
        {
            var request = UnitTestFixture.CreateRequestMock($"{method} /a HTTP/1.1\r\n\r\n");

            Assert.Equal(WebMessage.RequestMethod.NONE, request.Method);
        }
    }
}
