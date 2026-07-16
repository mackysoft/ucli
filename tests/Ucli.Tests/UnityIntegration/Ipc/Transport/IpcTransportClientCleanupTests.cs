using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientCleanupTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenResponseIsCompleteAndStreamCleanupBlocks_ReturnsResponseBeforeCleanupCompletes ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var connector = new BlockingDisposeConnector(new IpcTransportConnector());
        var client = new IpcTransportClient(connector, TimeProvider.System);

        await IpcTransportTestHarness.WithUnixResponseServerAsync(
            static async (request, stream, cancellationToken) =>
            {
                await MackySoft.Ucli.Infrastructure.Ipc.IpcFrameCodec.WriteModelAsync(
                    stream,
                    IpcTransportTestHarness.CreateResponse(request.RequestId, """{"done":true}"""),
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
            },
            async (endpoint, request) =>
            {
                var sendTask = client.SendAsync(
                        endpoint,
                        request,
                        IpcTransportClientTestSupport.DefaultTimeout)
                    .AsTask();

                await connector.DisposeStarted.WaitAsync(IpcTransportClientTestSupport.WaitTimeout);
                var response = await sendTask.WaitAsync(IpcTransportClientTestSupport.WaitTimeout);

                Assert.Equal(request.RequestId, response.RequestId);
                Assert.False(connector.DisposeCompleted.IsCompleted);

                connector.ReleaseDispose();
                await connector.DisposeCompleted.WaitAsync(IpcTransportClientTestSupport.WaitTimeout);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }

    private sealed class BlockingDisposeConnector : IIpcTransportConnector
    {
        private readonly IIpcTransportConnector innerConnector;

        private readonly TaskCompletionSource disposeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposeRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposeCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingDisposeConnector (IIpcTransportConnector innerConnector)
        {
            this.innerConnector = innerConnector ?? throw new ArgumentNullException(nameof(innerConnector));
        }

        public Task DisposeStarted => disposeStarted.Task;

        public Task DisposeCompleted => disposeCompleted.Task;

        public async ValueTask<Stream> ConnectAsync (
            IpcEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            var connectedStream = await innerConnector.ConnectAsync(endpoint, cancellationToken);
            return new BlockingDisposeStream(
                connectedStream,
                disposeStarted,
                disposeRelease,
                disposeCompleted);
        }

        public void ReleaseDispose ()
        {
            disposeRelease.TrySetResult();
        }
    }

    private sealed class BlockingDisposeStream : Stream
    {
        private readonly Stream innerStream;

        private readonly TaskCompletionSource disposeStarted;

        private readonly TaskCompletionSource disposeRelease;

        private readonly TaskCompletionSource disposeCompleted;

        public BlockingDisposeStream (
            Stream innerStream,
            TaskCompletionSource disposeStarted,
            TaskCompletionSource disposeRelease,
            TaskCompletionSource disposeCompleted)
        {
            this.innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            this.disposeStarted = disposeStarted ?? throw new ArgumentNullException(nameof(disposeStarted));
            this.disposeRelease = disposeRelease ?? throw new ArgumentNullException(nameof(disposeRelease));
            this.disposeCompleted = disposeCompleted ?? throw new ArgumentNullException(nameof(disposeCompleted));
        }

        public override bool CanRead => innerStream.CanRead;

        public override bool CanSeek => innerStream.CanSeek;

        public override bool CanWrite => innerStream.CanWrite;

        public override long Length => innerStream.Length;

        public override long Position
        {
            get => innerStream.Position;
            set => innerStream.Position = value;
        }

        public override void Flush ()
        {
            innerStream.Flush();
        }

        public override Task FlushAsync (CancellationToken cancellationToken)
        {
            return innerStream.FlushAsync(cancellationToken);
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            return innerStream.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return innerStream.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek (
            long offset,
            SeekOrigin origin)
        {
            return innerStream.Seek(offset, origin);
        }

        public override void SetLength (long value)
        {
            innerStream.SetLength(value);
        }

        public override void Write (
            byte[] buffer,
            int offset,
            int count)
        {
            innerStream.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return innerStream.WriteAsync(buffer, cancellationToken);
        }

        public override async ValueTask DisposeAsync ()
        {
            disposeStarted.TrySetResult();
            await disposeRelease.Task.ConfigureAwait(false);
            try
            {
                await innerStream.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                disposeCompleted.TrySetResult();
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                innerStream.Dispose();
                disposeCompleted.TrySetResult();
            }

            base.Dispose(disposing);
        }
    }
}
