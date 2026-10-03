using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponsePayloadTooLarge class.
    /// </summary>
    public class UnitTestResponsePayloadTooLarge
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponsePayloadTooLarge();

            // validation
            Assert.Equal(413, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponsePayloadTooLarge();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("413 -", response.Content as string);
        }
    }
}
