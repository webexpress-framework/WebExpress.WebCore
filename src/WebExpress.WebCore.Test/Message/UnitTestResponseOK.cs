using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseOK class.
    /// </summary>
    public class UnitTestResponseOK
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseOK();

            // validation
            Assert.Equal(200, response.Status);
        }

        /// <summary>
        /// Tests that the reason phrase is set on construction.
        /// </summary>
        [Fact]
        public void Reason()
        {
            // act
            var response = new ResponseOK();

            // validation
            Assert.Equal("OK", response.Reason);
        }
    }
}
