using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseGatewayTimeout class.
    /// </summary>
    public class UnitTestResponseGatewayTimeout
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseGatewayTimeout();

            // validation
            Assert.Equal(504, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponseGatewayTimeout();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("504 -", response.Content as string);
        }
    }
}
