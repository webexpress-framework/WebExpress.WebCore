using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseMovedPermanently class.
    /// </summary>
    public class UnitTestResponseMovedPermanently
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseMovedPermanently();

            // validation
            Assert.Equal(301, response.Status);
        }

        /// <summary>
        /// Tests that the target location is written to the Location header.
        /// </summary>
        [Fact]
        public void Location()
        {
            // arrange
            var location = new UriEndpoint("/target");

            // act
            var response = new ResponseMovedPermanently(location);

            // validation
            Assert.Equal(location.ToString(), response.Header.Location);
        }
    }
}
