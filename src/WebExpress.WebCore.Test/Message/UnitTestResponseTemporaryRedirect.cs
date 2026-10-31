using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseTemporaryRedirect class.
    /// </summary>
    public class UnitTestResponseTemporaryRedirect
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseTemporaryRedirect();

            // validation
            Assert.Equal(307, response.Status);
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
            var response = new ResponseTemporaryRedirect(location);

            // validation
            Assert.Equal(location.ToString(), response.Header.Location);
        }

        /// <summary>
        /// Tests that the reason phrase is set regardless of the constructor used, so a
        /// response created without a location still writes a complete status line.
        /// </summary>
        [Fact]
        public void Reason()
        {
            // act
            var withoutLocation = new ResponseTemporaryRedirect();
            var withLocation = new ResponseTemporaryRedirect(new UriEndpoint("/target"));

            // validation
            Assert.Equal("Temporary Redirect", withoutLocation.Reason);
            Assert.Equal("Temporary Redirect", withLocation.Reason);
        }
    }
}
