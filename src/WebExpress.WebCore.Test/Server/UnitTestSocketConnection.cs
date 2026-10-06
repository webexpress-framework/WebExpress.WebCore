using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebSocket;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Tests the server side of a WebSocket connection.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSocketConnection
    {
        /// <summary>
        /// Two senders writing to the same connection at once - a metrics timer and a request
        /// handler, say - both succeed and never write to the stream at the same time, so the
        /// second one cannot fail and have a healthy client dropped as broken.
        /// </summary>
        [Fact]
        public async Task ConcurrentSendsAreSerialized()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationContext = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var socketContext = componentHub.SocketManager.GetSockets<TestSocketA>(applicationContext).First();
            var stream = new GatedStream();
            using var connection = new SocketConnection(stream, socketContext);

            // act
            var first = connection.SendTextAsync("first", TestContext.Current.CancellationToken);
            var second = connection.SendTextAsync("second", TestContext.Current.CancellationToken);
            stream.Open();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(1, stream.MaxConcurrentWrites);
            Assert.Equal(2, stream.Writes);
        }

        /// <summary>
        /// A socket that declares no subprotocol negotiates none in the handshake; the
        /// connection must accept that rather than fail on the empty value after the upgrade.
        /// </summary>
        [Fact]
        public void SocketWithoutSubProtocolConnects()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var applicationContext = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var socketContext = componentHub.SocketManager.GetSockets<TestSocketA>(applicationContext).First();

            // act
            using var connection = new SocketConnection(new GatedStream(), socketContext);

            // validation
            Assert.True(string.IsNullOrEmpty(socketContext.SupportedSubProtocol));
            Assert.NotNull(connection);
        }

        /// <summary>
        /// A write-only stream whose writes stay pending until the test opens it, and that
        /// records how many writes overlapped.
        /// </summary>
        private sealed class GatedStream : Stream
        {
            private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _active;
            private int _maxConcurrentWrites;
            private int _writes;

            public int MaxConcurrentWrites => Volatile.Read(ref _maxConcurrentWrites);

            public int Writes => Volatile.Read(ref _writes);

            public void Open() => _gate.TrySetResult();

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var active = Interlocked.Increment(ref _active);
                InterlockedMax(ref _maxConcurrentWrites, active);

                try
                {
                    await _gate.Task.WaitAsync(cancellationToken);
                    Interlocked.Increment(ref _writes);
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

            private static void InterlockedMax(ref int target, int value)
            {
                int current;

                while ((current = Volatile.Read(ref target)) < value)
                {
                    if (Interlocked.CompareExchange(ref target, value, current) == current)
                    {
                        return;
                    }
                }
            }
        }
    }
}
