using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ResponseSwitchingProtocols class.
    /// </summary>
    public class UnitTestResponseSwitchingProtocols
    {
        /// <summary>
        /// Tests that the status code is derived from the StatusCode attribute.
        /// </summary>
        [Fact]
        public void StatusCode()
        {
            // act
            var response = new ResponseSwitchingProtocols("keep-alive", "websocket", "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=");

            // validation
            Assert.Equal(101, response.Status);
        }

        /// <summary>
        /// Tests that the handshake headers required for a protocol upgrade are set.
        /// </summary>
        [Fact]
        public void HandshakeHeaders()
        {
            // act
            var response = new ResponseSwitchingProtocols("keep-alive", "websocket", "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=");

            // validation
            Assert.Equal("keep-alive", response.Header.Connection);
            Assert.Equal("websocket", response.Header.Upgrade);
            Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", response.Header.SecWebSocketAccept);
        }
    }
}
