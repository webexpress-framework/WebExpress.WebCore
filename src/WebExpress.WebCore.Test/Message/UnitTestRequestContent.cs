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
        /// Tests that a non-positive content length yields no content.
        /// </summary>
        [Theory]
        [InlineData(0L)]
        [InlineData(-1L)]
        public async Task NonPositiveContentLength(long contentLength)
        {
            // act
            var content = await Request.GetContentAsync(new MemoryStream([1, 2, 3]), contentLength, CancellationToken.None);

            // validation
            Assert.Null(content);
        }

        /// <summary>
        /// Tests that a body sent without a content length - chunked over HTTP/1.1, or over
        /// HTTP/2 and HTTP/3 without the header - is read to the end of the stream instead of
        /// being dropped.
        /// </summary>
        [Fact]
        public async Task MissingContentLengthReadsToTheEnd()
        {
            // arrange
            var body = CreateBody(Request.InitialContentBufferSize + 5);

            // act
            var content = await Request.GetContentAsync(new FragmentedStream(body, 4096), null, CancellationToken.None);

            // validation
            Assert.Equal(body, content);
        }

        /// <summary>
        /// Tests that the body is read asynchronously, so a server that disallows synchronous IO
        /// still receives it.
        /// </summary>
        [Fact]
        public async Task BodyIsReadWithoutSynchronousIo()
        {
            // arrange
            var body = CreateBody(100);

            // act
            var content = await Request.GetContentAsync(new FragmentedStream(body, 7, allowSynchronousIo: false), body.Length, CancellationToken.None);

            // validation
            Assert.Equal(body, content);
        }

        /// <summary>
        /// Tests that a request without a body yields no content.
        /// </summary>
        [Fact]
        public async Task NoBody()
        {
            // act
            var content = await Request.GetContentAsync(null, null, CancellationToken.None);

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
        public async Task BodyLargerThanInitialBuffer(int maxReadSize)
        {
            // arrange
            var body = CreateBody(Request.InitialContentBufferSize * 3 + 17);

            // act
            var content = await Request.GetContentAsync(new FragmentedStream(body, maxReadSize), body.Length, CancellationToken.None);

            // validation
            Assert.Equal(body, content);
        }

        /// <summary>
        /// Tests that a client announcing a huge body but sending only a few bytes gets exactly
        /// those bytes, without the server reserving memory for the announced size.
        /// </summary>
        [Fact]
        public async Task AnnouncedLengthExceedsBody()
        {
            // arrange
            var body = CreateBody(5);

            // act
            var content = await Request.GetContentAsync(new MemoryStream(body), long.MaxValue, CancellationToken.None);

            // validation
            Assert.Equal(body, content);
        }

        /// <summary>
        /// Tests that bytes beyond the announced content length are not read.
        /// </summary>
        [Fact]
        public async Task BodyExceedsAnnouncedLength()
        {
            // arrange
            var body = CreateBody(100);

            // act
            var content = await Request.GetContentAsync(new MemoryStream(body), 40, CancellationToken.None);

            // validation
            Assert.Equal(body[..40], content);
        }

        /// <summary>
        /// Tests that an announced body that never arrives yields no content.
        /// </summary>
        [Fact]
        public async Task EmptyBody()
        {
            // act
            var content = await Request.GetContentAsync(new MemoryStream(), 1024, CancellationToken.None);

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
        /// stream does when the client sends its body in several packets. Like Kestrel's request
        /// body with AllowSynchronousIO off, it can refuse synchronous reads.
        /// </summary>
        private sealed class FragmentedStream(byte[] data, int maxReadSize, bool allowSynchronousIo = true)
            : MemoryStream(data, 0, data.Length, writable: false, publiclyVisible: true)
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!allowSynchronousIo)
                {
                    throw new InvalidOperationException("Synchronous operations are disallowed.");
                }

                return base.Read(buffer, offset, Math.Min(count, maxReadSize));
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                // copied by hand: the base implementation of a derived memory stream falls back
                // to the synchronous read
                var count = (int)Math.Min(Math.Min(buffer.Length, maxReadSize), Length - Position);
                GetBuffer().AsSpan((int)Position, count).CopyTo(buffer.Span);
                Position += count;

                return ValueTask.FromResult(count);
            }
        }
    }
}
