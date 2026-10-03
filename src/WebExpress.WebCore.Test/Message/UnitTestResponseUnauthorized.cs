using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseUnauthorized class.
    /// </summary>
    public class UnitTestResponseUnauthorized
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseUnauthorized();

            // validation
            Assert.Equal(401, response.Status);
        }

        /// <summary>
        /// Tests that the reason phrase is the standard one for 401 and not inherited from a success response.
        /// </summary>
        [Fact]
        public void Reason()
        {
            // act
            var response = new ResponseUnauthorized();

            // validation
            Assert.Equal("Unauthorized", response.Reason);
        }

        /// <summary>
        /// Tests that a supplied status message becomes the body instead of being dropped.
        /// </summary>
        [Fact]
        public void MessageContent()
        {
            // act
            var response = new ResponseUnauthorized(new StatusMessage("Authentication required."));

            // validation
            Assert.Equal("Authentication required.", response.Content);
            Assert.Equal("text/html", response.Header.ContentType);
        }

        /// <summary>
        /// Tests that no body is set without a message, so callers can attach their own content type.
        /// </summary>
        [Fact]
        public void NoMessageNoContent()
        {
            // act
            var response = new ResponseUnauthorized();

            // validation
            Assert.Null(response.Content);
            Assert.Null(response.Header.ContentType);
        }

        /// <summary>
        /// Tests that the WWW-Authenticate header is requested so the client is prompted to authenticate.
        /// </summary>
        [Fact]
        public void WwwAuthenticate()
        {
            // act
            var response = new ResponseUnauthorized();

            // validation
            Assert.True(response.Header.WWWAuthenticate);
        }
    }
}
