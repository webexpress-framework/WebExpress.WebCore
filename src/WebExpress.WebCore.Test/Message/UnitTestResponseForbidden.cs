using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseForbidden class.
    /// </summary>
    public class UnitTestResponseForbidden
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseForbidden();

            // validation
            Assert.Equal(403, response.Status);
        }

        /// <summary>
        /// Tests that a default html body and content type are produced when no message is supplied.
        /// </summary>
        [Fact]
        public void DefaultContent()
        {
            // act
            var response = new ResponseForbidden();

            // validation
            Assert.Equal("text/html", response.Header.ContentType);
            Assert.Contains("403 -", response.Content as string);
        }
    }
}
