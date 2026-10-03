using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseServiceUnavailable class.
    /// </summary>
    public class UnitTestResponseServiceUnavailable
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseServiceUnavailable();

            // validation
            Assert.Equal(503, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponseServiceUnavailable();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("503 -", response.Content as string);
        }
    }
}
