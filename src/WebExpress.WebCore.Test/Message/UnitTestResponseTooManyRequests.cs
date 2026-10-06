using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseTooManyRequests class.
    /// </summary>
    public class UnitTestResponseTooManyRequests
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseTooManyRequests();

            // validation
            Assert.Equal(429, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponseTooManyRequests();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("429 -", response.Content as string);
        }
    }
}
