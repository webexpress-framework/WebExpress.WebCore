using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseCreated class.
    /// </summary>
    public class UnitTestResponseCreated
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseCreated();

            // validation
            Assert.Equal(201, response.Status);
        }

        /// <summary>
        /// Tests that the reason phrase is set on construction.
        /// </summary>
        [Fact]
        public void Reason()
        {
            // act
            var response = new ResponseCreated();

            // validation
            Assert.Equal("Created", response.Reason);
        }
    }
}
