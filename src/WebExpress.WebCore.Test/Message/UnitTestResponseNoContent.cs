using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseNoContent class.
    /// </summary>
    public class UnitTestResponseNoContent
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseNoContent();

            // validation
            Assert.Equal(204, response.Status);
        }

        /// <summary>
        /// Tests that the reason phrase is set on construction.
        /// </summary>
        [Fact]
        public void Reason()
        {
            // act
            var response = new ResponseNoContent();

            // validation
            Assert.Equal("No Content", response.Reason);
        }
    }
}
