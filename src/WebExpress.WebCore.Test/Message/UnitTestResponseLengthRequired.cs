using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseLengthRequired class.
    /// </summary>
    public class UnitTestResponseLengthRequired
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseLengthRequired();

            // validation
            Assert.Equal(411, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponseLengthRequired();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("411 -", response.Content as string);
        }
    }
}
