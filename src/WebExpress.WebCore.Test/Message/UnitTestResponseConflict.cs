using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseConflict class.
    /// </summary>
    public class UnitTestResponseConflict
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseConflict();

            // validation
            Assert.Equal(409, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponseConflict();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("409 -", response.Content as string);
        }
    }
}
