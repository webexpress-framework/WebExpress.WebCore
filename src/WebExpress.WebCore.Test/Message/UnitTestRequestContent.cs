using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for reading the request body, which is sized by the client-supplied
    /// Content-Length header and therefore must not be trusted for the allocation.
    /// </summary>
    public class UnitTestRequestContent
    {
        /// <summary>
        /// Tests that a missing or non-positive content length yields no content.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData(0L)]
        [InlineData(-1L)]
        public void NoContentLength(long? contentLength)
        {
            // act
            var content = Request.GetContent(new MemoryStream([1, 2, 3]), contentLength);

            // validation
            Assert.Null(content);
        }

        /// <summary>
        /// Tests that a body larger than the initial buffer is read completely, also when the
        /// stream delivers it in small fragments across several buffer resizes.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(4096)]
        [InlineData(Request.InitialContentBufferSize)]
        public void BodyLargerThanInitialBuffer(int maxReadSize)
        {
            // arrange
            var body = CreateBody(Request.InitialContentBufferSize * 3 + 17);

            // act
            var content = Request.GetContent(new FragmentedStream(body, maxReadSize), body.Length);

            // validation
            Assert.Equal(body, content);
        }

        /// <summary>
        /// Tests that a client announcing a huge body but sending only a few bytes gets exactly
        /// those bytes, without the server reserving memory for the announced size.
        /// </summary>
        [Fact]
        public void AnnouncedLengthExceedsBody()
        {
            // arrange
            var body = CreateBody(5);

            // act
            var content = Request.GetContent(new MemoryStream(body), long.MaxValue);

            // validation
            Assert.Equal(body, content);
        }

        /// <summary>
        /// Tests that bytes beyond the announced content length are not read.
        /// </summary>
        [Fact]
        public void BodyExceedsAnnouncedLength()
        {
            // arrange
            var body = CreateBody(100);

            // act
            var content = Request.GetContent(new MemoryStream(body), 40);

            // validation
            Assert.Equal(body[..40], content);
        }

        /// <summary>
        /// Tests that an announced body that never arrives yields no content.
        /// </summary>
        [Fact]
        public void EmptyBody()
        {
            // act
            var content = Request.GetContent(new MemoryStream(), 1024);

            // validation
            Assert.Null(content);
        }

        /// <summary>
        /// Creates a body with a non-repeating byte pattern so misplaced fragments are detected.
        /// </summary>
        /// <param name="length">The length of the body.</param>
        /// <returns>The body.</returns>
        private static byte[] CreateBody(int length)
        {
            var body = new byte[length];
            new Random(length).NextBytes(body);

            return body;
        }

        /// <summary>
        /// A stream that returns at most a fixed number of bytes per read, as a network
        /// stream does when the client sends its body in several packets.
        /// </summary>
        private sealed class FragmentedStream(byte[] data, int maxReadSize) : MemoryStream(data)
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                return base.Read(buffer, offset, Math.Min(count, maxReadSize));
            }
        }
    }
}
