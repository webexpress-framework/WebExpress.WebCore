using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Tests that the parameters a client sends are bounded, so one request cannot grow the
    /// parameter dictionary without limit.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestRequestLimits
    {
        /// <summary>
        /// Verifies that keys beyond the limit are dropped while a repeated key still replaces
        /// its earlier value.
        /// </summary>
        [Fact]
        public void DistinctParametersAreBounded()
        {
            // arrange
            _ = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var query = string.Join("&", Enumerable.Range(0, RequestBase.MaxParameters + 500).Select(x => $"k{x}=v"));

            // act
            var request = UnitTestFixture.CreateRequestMock($"GET /?{query}&k0=last HTTP/1.1\r\n\r\n");

            // validation
            Assert.Equal(RequestBase.MaxParameters, request.Parameters.Count());
            Assert.Equal("last", request.GetParameter("k0")?.Value);
        }
    }
}
