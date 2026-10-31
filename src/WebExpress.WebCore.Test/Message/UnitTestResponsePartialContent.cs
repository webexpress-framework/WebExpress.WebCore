using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponsePartialContent class.
    /// </summary>
    public class UnitTestResponsePartialContent
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponsePartialContent();

            // validation
            Assert.Equal(206, response.Status);
        }

        /// <summary>
        /// Tests that the reason phrase is set on construction.
        /// </summary>
        [Fact]
        public void Reason()
        {
            // act
            var response = new ResponsePartialContent();

            // validation
            Assert.Equal("Partial Content", response.Reason);
        }
    }
}
