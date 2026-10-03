using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests that pin the reason phrase of every response type to the standard phrase of its
    /// status code, because the phrase is sent on the HTTP/1.1 status line.
    /// </summary>
    public class UnitTestResponseReason
    {
        /// <summary>
        /// Tests that the reason phrase matches the standard phrase for the status code.
        /// </summary>
        [Theory]
        [InlineData(typeof(ResponseOK), "OK")]
        [InlineData(typeof(ResponseCreated), "Created")]
        [InlineData(typeof(ResponseAccepted), "Accepted")]
        [InlineData(typeof(ResponseNoContent), "No Content")]
        [InlineData(typeof(ResponsePartialContent), "Partial Content")]
        [InlineData(typeof(ResponseMovedPermanently), "Moved Permanently")]
        [InlineData(typeof(ResponseMovedTemporarily), "Found")]
        [InlineData(typeof(ResponseSeeOther), "See Other")]
        [InlineData(typeof(ResponseNotModified), "Not Modified")]
        [InlineData(typeof(ResponseTemporaryRedirect), "Temporary Redirect")]
        [InlineData(typeof(ResponsePermanentRedirect), "Permanent Redirect")]
        [InlineData(typeof(ResponseBadRequest), "Bad Request")]
        [InlineData(typeof(ResponseUnauthorized), "Unauthorized")]
        [InlineData(typeof(ResponseForbidden), "Forbidden")]
        [InlineData(typeof(ResponseNotFound), "Not Found")]
        [InlineData(typeof(ResponseMethodNotAllowed), "Method Not Allowed")]
        [InlineData(typeof(ResponseNotAcceptable), "Not Acceptable")]
        [InlineData(typeof(ResponseRequestTimeout), "Request Timeout")]
        [InlineData(typeof(ResponseConflict), "Conflict")]
        [InlineData(typeof(ResponseGone), "Gone")]
        [InlineData(typeof(ResponseLengthRequired), "Length Required")]
        [InlineData(typeof(ResponsePayloadTooLarge), "Payload Too Large")]
        [InlineData(typeof(ResponseUnsupportedMediaType), "Unsupported Media Type")]
        [InlineData(typeof(ResponseUnprocessableEntity), "Unprocessable Entity")]
        [InlineData(typeof(ResponseUpgradeRequired), "Upgrade Required")]
        [InlineData(typeof(ResponsePreconditionRequired), "Precondition Required")]
        [InlineData(typeof(ResponseTooManyRequests), "Too Many Requests")]
        [InlineData(typeof(ResponseInternalServerError), "Internal Server Error")]
        [InlineData(typeof(ResponseNotImplemented), "Not Implemented")]
        [InlineData(typeof(ResponseBadGateway), "Bad Gateway")]
        [InlineData(typeof(ResponseServiceUnavailable), "Service Unavailable")]
        [InlineData(typeof(ResponseGatewayTimeout), "Gateway Timeout")]
        [InlineData(typeof(ResponseHttpVersionNotSupported), "HTTP Version Not Supported")]
        public void Reason(Type responseType, string expected)
        {
            // act
            var response = (Response)Activator.CreateInstance(responseType);

            // validation
            Assert.Equal(expected, response.Reason);
        }
    }
}
