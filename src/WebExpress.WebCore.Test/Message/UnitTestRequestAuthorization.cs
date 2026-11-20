using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for parsing the Authorization header with <see cref="RequestAuthorization"/>.
    /// </summary>
    public class UnitTestRequestAuthorization
    {
        /// <summary>
        /// Tests that the user id of Basic credentials ends at the first colon and the password
        /// keeps every further one (RFC 7617).
        /// </summary>
        /// <param name="credentials">The decoded credentials.</param>
        /// <param name="identification">The expected user id.</param>
        /// <param name="password">The expected password.</param>
        [Theory]
        [InlineData("alice:secret", "alice", "secret")]
        [InlineData("alice:pa:ss:word", "alice", "pa:ss:word")]
        [InlineData("alice:", "alice", "")]
        [InlineData("alice", "alice", null)]
        public void BasicSplitsAtTheFirstColon(string credentials, string identification, string password)
        {
            // arrange
            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));

            // act
            var authorization = RequestAuthorization.Parse($"Basic {encoded}");

            // validation
            Assert.Equal("Basic", authorization.Type);
            Assert.Equal(identification, authorization.Identification);
            Assert.Equal(password, authorization.Password);
        }
    }
}
