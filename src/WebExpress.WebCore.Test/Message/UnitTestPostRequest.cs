using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for parsing HTTP POST requests.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestPostRequest : UnitTestRequest
    {
        /// <summary>
        /// Tests parsing of a POST request with <c>text/plain</c> payload.
        /// </summary>
        [Fact]
        public void TextPlain()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeTextPlain.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("1", request?.GetParameter("submit_manufactor")?.Value);
        }

        /// <summary>
        /// Tests parsing of a POST request with <c>text/plain</c> payload containing umlauts.
        /// </summary>
        [Fact]
        public void TextPlainWithUmlaut()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeTextPlain_Umlaut.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("ä", request?.GetParameter("a")?.Value);
            Assert.Equal("ö ü", request?.GetParameter("b")?.Value);
            Assert.Equal("1", request?.GetParameter("submit_")?.Value);
        }

        /// <summary>
        /// Tests parsing of a POST request with <c>application/x-www-form-urlencoded</c> payload.
        /// </summary>
        [Fact]
        public void Urlencoded()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeXwwwFormUrlencoded.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("1", request?.GetParameter("submit_manufactor")?.Value?.Trim());
        }

        /// <summary>
        /// Tests parsing of a URL-encoded POST request containing umlauts.
        /// </summary>
        [Fact]
        public void UrlencodedWithUmlaut()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeXwwwFormUrlencoded_umlaut.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("ä", GetParameterValue(request, "a"));
            Assert.Equal("ö ü", GetParameterValue(request, "b"));
            Assert.Equal("1", GetParameterValue(request, "submit_")?.Trim());
        }


        [Fact]
        public void Multipart1()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeMultipartFormData1.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("1", request?.GetParameter("submit_manufactor")?.Value);
        }

        /// <summary>
        /// Tests parsing of a multipart POST request including a file upload.
        /// </summary>
        [Fact]
        public void Multipart2()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeMultipartFormData2.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("1", request?.GetParameter("submit_manufactor")?.Value);

            var file = request?.GetParameter("image") as ParameterFile;
            Assert.NotNull(file);
            Assert.Equal("Unbenannt.png", file.Value);
            Assert.Equal("image/png", file.ContentType);
            Assert.True((file.Data?.Length ?? 0) > 0);
        }

        /// <summary>
        /// Tests parsing of a multipart POST request for delete submit action.
        /// </summary>
        [Fact]
        public void Multipart3()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeMultipartFormData3.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("1", request?.GetParameter("submit_del")?.Value);
        }

        /// <summary>
        /// Tests parsing of a multipart POST request with hyphenated field names.
        /// </summary>
        [Fact]
        public void Multipart4()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeMultipartFormData4.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("1", request?.GetParameter("submit-formular-inventory")?.Value);
        }

        /// <summary>
        /// Tests parsing of a multipart POST request containing umlauts.
        /// </summary>
        [Fact]
        public void MultipartWithUmlaut()
        {
            var content = UnitTestFixture.GetEmbeddedResource("contentTypeMultipartFormData_Umlaut.post");
            var request = UnitTestFixture.CreateRequestMock(content);

            Assert.Equal("ä", request?.GetParameter("a")?.Value);
            Assert.Equal("ö ü", request?.GetParameter("b")?.Value);
            Assert.Equal("1", request?.GetParameter("submit_")?.Value);
        }

        /// <summary>
        /// Tests that a multipart field arrives as it was typed: the body is not url-encoded,
        /// so a plus or a percent sign is no escape there.
        /// </summary>
        [Fact]
        public void MultipartKeepsWhatWasTyped()
        {
            var body = "------B\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nC++ 100% a+b%2B\r\n------B--\r\n";

            var request = Post("multipart/form-data; boundary=----B", body);

            Assert.Equal("C++ 100% a+b%2B", request?.GetParameter("a")?.Value);
        }

        /// <summary>
        /// Tests that a plain text field arrives as it was typed.
        /// </summary>
        [Fact]
        public void TextPlainKeepsWhatWasTyped()
        {
            var request = Post("text/plain", "a=C++ 100%\r\n");

            Assert.Equal("C++ 100%", request?.GetParameter("a")?.Value);
        }

        /// <summary>
        /// Tests that an url-encoded body is decoded: a plus is a blank, an escape its character.
        /// </summary>
        [Fact]
        public void UrlencodedDecodesItsEscapes()
        {
            var request = Post("application/x-www-form-urlencoded", "a=C%2B%2B&b=a+b&c=100%25");

            Assert.Equal("C++", GetParameterValue(request, "a"));
            Assert.Equal("a b", GetParameterValue(request, "b"));
            Assert.Equal("100%", GetParameterValue(request, "c")?.Trim());
        }

        /// <summary>
        /// Tests that the keys of an url-encoded body are decoded as their values are.
        /// </summary>
        [Fact]
        public void UrlencodedDecodesItsKeys()
        {
            var request = Post("application/x-www-form-urlencoded", "items%5B%5D=a&first+name=b");

            Assert.Equal("a", GetParameterValue(request, "items[]"));
            Assert.Equal("b", GetParameterValue(request, "first name")?.Trim());
        }

        /// <summary>
        /// Builds a POST request with the given body.
        /// </summary>
        /// <param name="contentType">The content type of the body.</param>
        /// <param name="body">The body as it travels.</param>
        /// <returns>The parsed request.</returns>
        private static IRequest Post(string contentType, string body)
        {
            var content = $"POST /ix HTTP/1.1\r\nHost: localhost\r\nContent-Length: {System.Text.Encoding.UTF8.GetByteCount(body)}\r\nContent-Type: {contentType}\r\n\r\n{body}";

            return UnitTestFixture.CreateRequestMock(content);
        }

        /// <summary>
        /// Retrieves the value of a parameter from the request using the
        /// specified key.
        /// </summary>
        /// <param name="request">
        /// The request from which the parameter value should be retrieved.
        /// </param>
        /// <param name="key">
        /// The key of the parameter.
        /// </param>
        /// <returns>
        /// The parameter value, or <see langword="null"/> if no parameter with 
        /// the specified key is found.
        /// </returns>
        private static string GetParameterValue(IRequest request, string key)
        {
            return request?.GetParameter(key)?.Value
                ?? request?.Parameters?
                    .OfType<Parameter>()
                    .FirstOrDefault(x => string.Equals(x.Key?.Trim('\r', '\n', '\uFEFF'), key, StringComparison.OrdinalIgnoreCase))?
                    .Value;
        }

    }
}
