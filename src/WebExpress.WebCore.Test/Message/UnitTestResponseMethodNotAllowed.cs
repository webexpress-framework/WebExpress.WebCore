using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseMethodNotAllowed class.
    /// </summary>
    public class UnitTestResponseMethodNotAllowed
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseMethodNotAllowed();

            // validation
            Assert.Equal(405, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponseMethodNotAllowed();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("405 -", response.Content as string);
        }
    }
}
