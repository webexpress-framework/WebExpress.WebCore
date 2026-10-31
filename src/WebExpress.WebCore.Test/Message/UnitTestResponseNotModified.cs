using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseNotModified class.
    /// </summary>
    public class UnitTestResponseNotModified
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseNotModified();

            // validation
            Assert.Equal(304, response.Status);
        }

        /// <summary>
        /// Tests that the reason phrase is set on construction.
        /// </summary>
        [Fact]
        public void Reason()
        {
            // act
            var response = new ResponseNotModified();

            // validation
            Assert.Equal("Not Modified", response.Reason);
        }
    }
}
