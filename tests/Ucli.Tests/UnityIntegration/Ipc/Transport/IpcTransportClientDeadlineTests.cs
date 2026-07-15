using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientDeadlineTests
{
    private static readonly TimeSpan TransportTimeout = TimeSpan.FromSeconds(1);

    public static TheoryData<RequestKind, BlockingStage> BoundedPhaseCases => new()
    {
        { RequestKind.Single, BlockingStage.Connect },
        { RequestKind.Single, BlockingStage.Write },
        { RequestKind.Single, BlockingStage.Read },
        { RequestKind.Streaming, BlockingStage.Connect },
        { RequestKind.Streaming, BlockingStage.Write },
        { RequestKind.Streaming, BlockingStage.Read },
        { RequestKind.SingleWithUnboundedResponseWait, BlockingStage.Connect },
        { RequestKind.SingleWithUnboundedResponseWait, BlockingStage.Write },
        { RequestKind.StreamingWithUnboundedResponseWait, BlockingStage.Connect },
        { RequestKind.StreamingWithUnboundedResponseWait, BlockingStage.Write },
    };

    public static TheoryData<RequestKind> AllRequestKinds => new()
    {
        RequestKind.Single,
        RequestKind.Streaming,
        RequestKind.SingleWithUnboundedResponseWait,
        RequestKind.StreamingWithUnboundedResponseWait,
    };

    public static TheoryData<RequestKind> UnboundedResponseWaitKinds => new()
    {
        RequestKind.SingleWithUnboundedResponseWait,
        RequestKind.StreamingWithUnboundedResponseWait,
    };

    [Theory]
    [MemberData(nameof(AllRequestKinds))]
    [Trait("Size", "Small")]
    public async Task Send_WhenConnectionOutlivesConnectionAttemptCap_FailsBeforeOverallRequestDeadline (
        RequestKind requestKind)
    {
        var timeProvider = new ManualTimeProvider();
        var stream = new NonCooperativeStream(BlockingStage.Connect);
        var connector = new NonCooperativeConnector(BlockingStage.Connect, stream);
        var client = new IpcTransportClient(connector, timeProvider);
        var overallTimeout = TimeSpan.FromSeconds(30);
        var sendTask = StartSend(client, requestKind, overallTimeout, CancellationToken.None);

        try
        {
            await TestAwaiter.WaitAsync(
                connector.WaitForStageAsync(BlockingStage.Connect),
                $"{requestKind} connection attempt start",
                IpcTransportClientTestSupport.WaitTimeout);
            await TestAwaiter.WaitAsync(
                timeProvider.WaitForTimerDueWithinAsync(IpcTransportClient.ConnectionAttemptTimeoutCap),
                $"{requestKind} connection-attempt deadline registration",
                IpcTransportClientTestSupport.WaitTimeout);

            timeProvider.Advance(IpcTransportClient.ConnectionAttemptTimeoutCap);

            var exception = await TestAwaiter.WaitAsync(
                Assert.ThrowsAsync<IpcConnectTimeoutException>(async () => await sendTask),
                $"{requestKind} connection-attempt timeout",
                IpcTransportClientTestSupport.WaitTimeout);

            Assert.Contains("before the request was sent", exception.Message, StringComparison.Ordinal);
            connector.CompleteLateConnection();
            await TestAwaiter.WaitAsync(
                stream.Disposed,
                $"{requestKind} late connection cleanup",
                IpcTransportClientTestSupport.WaitTimeout);
        }
        finally
        {
            connector.CompleteLateConnection();
            stream.ReleaseBlockedOperations();
        }
    }

    [Theory]
    [MemberData(nameof(BoundedPhaseCases))]
    [Trait("Size", "Small")]
    public async Task Send_WhenBoundedTransportPhaseIgnoresCancellation_ReturnsAtDeadlineAndAbortsOwnedStream (
        RequestKind requestKind,
        BlockingStage blockingStage)
    {
        var timeProvider = new ManualTimeProvider();
        var stream = new NonCooperativeStream(blockingStage);
        var connector = new NonCooperativeConnector(blockingStage, stream);
        var client = new IpcTransportClient(connector, timeProvider);
        var sendTask = StartSend(client, requestKind, TransportTimeout, CancellationToken.None);

        try
        {
            var deadlineRegisteredTask = timeProvider.WaitForTimerDueWithinAsync(TransportTimeout);
            await TestAwaiter.WaitAsync(
                connector.WaitForStageAsync(blockingStage),
                $"non-cooperative {blockingStage} start",
                IpcTransportClientTestSupport.WaitTimeout);
            await TestAwaiter.WaitAsync(
                deadlineRegisteredTask,
                "IPC transport deadline registration",
                IpcTransportClientTestSupport.WaitTimeout);

            timeProvider.Advance(TransportTimeout);

            var exceptionTask = Assert.ThrowsAnyAsync<TimeoutException>(async () => await sendTask);
            var exception = await TestAwaiter.WaitAsync(
                exceptionTask,
                $"{requestKind} {blockingStage} outward deadline",
                IpcTransportClientTestSupport.WaitTimeout);

            if (blockingStage == BlockingStage.Connect)
            {
                Assert.IsType<IpcConnectTimeoutException>(exception);
                connector.CompleteLateConnection();
            }
            else
            {
                Assert.IsType<TimeoutException>(exception);
            }

            await TestAwaiter.WaitAsync(
                stream.Disposed,
                $"{requestKind} {blockingStage} stream abort",
                IpcTransportClientTestSupport.WaitTimeout);
        }
        finally
        {
            connector.CompleteLateConnection();
            stream.ReleaseBlockedOperations();
        }
    }

    [Theory]
    [MemberData(nameof(AllRequestKinds))]
    [Trait("Size", "Small")]
    public async Task Send_WhenCallerCancelsNonCooperativeRead_ReturnsCallerCancellationAndAbortsOwnedStream (
        RequestKind requestKind)
    {
        var timeProvider = new ManualTimeProvider();
        var stream = new NonCooperativeStream(BlockingStage.Read);
        var connector = new NonCooperativeConnector(BlockingStage.Read, stream);
        var client = new IpcTransportClient(connector, timeProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        var sendTask = StartSend(
            client,
            requestKind,
            TransportTimeout,
            cancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(
                stream.ReadStarted,
                $"{requestKind} non-cooperative read start",
                IpcTransportClientTestSupport.WaitTimeout);

            cancellationTokenSource.Cancel();

            var exceptionTask = Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
            var exception = await TestAwaiter.WaitAsync(
                exceptionTask,
                $"{requestKind} caller cancellation",
                IpcTransportClientTestSupport.WaitTimeout);

            Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
            await TestAwaiter.WaitAsync(
                stream.Disposed,
                $"{requestKind} caller-canceled stream abort",
                IpcTransportClientTestSupport.WaitTimeout);
        }
        finally
        {
            stream.ReleaseBlockedOperations();
        }
    }

    [Theory]
    [MemberData(nameof(UnboundedResponseWaitKinds))]
    [Trait("Size", "Small")]
    public async Task SendWithUnboundedResponseWait_WhenReadIgnoresCancellation_DoesNotApplySendDeadlineToRead (
        RequestKind requestKind)
    {
        var timeProvider = new ManualTimeProvider();
        var stream = new NonCooperativeStream(BlockingStage.Read);
        var connector = new NonCooperativeConnector(BlockingStage.Read, stream);
        var client = new IpcTransportClient(connector, timeProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        var deadlineRegisteredTask = timeProvider.WaitForTimerDueWithinAsync(TransportTimeout);
        var sendTask = StartSend(
            client,
            requestKind,
            TransportTimeout,
            cancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(
                deadlineRegisteredTask,
                "unbounded response send-deadline registration",
                IpcTransportClientTestSupport.WaitTimeout);
            await TestAwaiter.WaitAsync(
                stream.ReadStarted,
                $"{requestKind} unbounded response read start",
                IpcTransportClientTestSupport.WaitTimeout);
            await TestAwaiter.WaitAsync(
                WaitForNoActiveTimerAsync(timeProvider),
                $"{requestKind} send deadline release",
                IpcTransportClientTestSupport.WaitTimeout);

            timeProvider.Advance(TransportTimeout);
            await Task.Yield();

            Assert.False(sendTask.IsCompleted);

            cancellationTokenSource.Cancel();
            var cancellationTask = Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
            await TestAwaiter.WaitAsync(
                cancellationTask,
                $"{requestKind} unbounded response caller cancellation",
                IpcTransportClientTestSupport.WaitTimeout);
        }
        finally
        {
            stream.ReleaseBlockedOperations();
        }
    }

    private static async Task WaitForNoActiveTimerAsync (ManualTimeProvider timeProvider)
    {
        while (timeProvider.ActiveTimerCount != 0)
        {
            await Task.Yield();
        }
    }

    private static Task<IpcResponse> StartSend (
        IpcTransportClient client,
        RequestKind requestKind,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "test-transport");
        return requestKind switch
        {
            RequestKind.Single => client.SendAsync(
                    endpoint,
                    IpcTransportTestHarness.CreateSingleRequest(),
                    timeout,
                    cancellationToken)
                .AsTask(),
            RequestKind.Streaming => client.SendStreamingAsync(
                    endpoint,
                    IpcTransportTestHarness.CreateStreamingRequest(),
                    timeout,
                    static (_, _) => ValueTask.CompletedTask,
                    cancellationToken)
                .AsTask(),
            RequestKind.SingleWithUnboundedResponseWait => client.SendWithUnboundedResponseWaitAsync(
                    endpoint,
                    IpcTransportTestHarness.CreateSingleRequest(),
                    timeout,
                    cancellationToken)
                .AsTask(),
            RequestKind.StreamingWithUnboundedResponseWait => client.SendStreamingWithUnboundedResponseWaitAsync(
                    endpoint,
                    IpcTransportTestHarness.CreateStreamingRequest(),
                    timeout,
                    static (_, _) => ValueTask.CompletedTask,
                    cancellationToken)
                .AsTask(),
            _ => throw new ArgumentOutOfRangeException(nameof(requestKind)),
        };
    }

    public enum RequestKind
    {
        Single,
        Streaming,
        SingleWithUnboundedResponseWait,
        StreamingWithUnboundedResponseWait,
    }

    public enum BlockingStage
    {
        Connect,
        Write,
        Read,
    }

    private sealed class NonCooperativeConnector : IIpcTransportConnector
    {
        private readonly BlockingStage blockingStage;

        private readonly NonCooperativeStream stream;

        private readonly TaskCompletionSource connectStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<Stream> connectionCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public NonCooperativeConnector (
            BlockingStage blockingStage,
            NonCooperativeStream stream)
        {
            this.blockingStage = blockingStage;
            this.stream = stream;
        }

        public ValueTask<Stream> ConnectAsync (
            IpcEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            _ = endpoint;
            _ = cancellationToken;
            connectStarted.TrySetResult();
            return blockingStage == BlockingStage.Connect
                ? new ValueTask<Stream>(connectionCompletion.Task)
                : ValueTask.FromResult<Stream>(stream);
        }

        public Task WaitForStageAsync (BlockingStage stage)
        {
            return stage switch
            {
                BlockingStage.Connect => connectStarted.Task,
                BlockingStage.Write => stream.WriteStarted,
                BlockingStage.Read => stream.ReadStarted,
                _ => throw new ArgumentOutOfRangeException(nameof(stage)),
            };
        }

        public void CompleteLateConnection ()
        {
            connectionCompletion.TrySetResult(stream);
        }
    }

    private sealed class NonCooperativeStream : Stream
    {
        private readonly BlockingStage blockingStage;

        private readonly TaskCompletionSource writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource writeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<int> readCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public NonCooperativeStream (BlockingStage blockingStage)
        {
            this.blockingStage = blockingStage;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public Task WriteStarted => writeStarted.Task;

        public Task ReadStarted => readStarted.Task;

        public Task Disposed => disposed.Task;

        public override void Flush ()
        {
        }

        public override Task FlushAsync (CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }

        public override Task<int> ReadAsync (
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            _ = buffer;
            _ = offset;
            _ = count;
            _ = cancellationToken;
            readStarted.TrySetResult();
            return readCompletion.Task;
        }

        public override ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _ = buffer;
            _ = cancellationToken;
            readStarted.TrySetResult();
            return new ValueTask<int>(readCompletion.Task);
        }

        public override long Seek (
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength (long value)
        {
            throw new NotSupportedException();
        }

        public override void Write (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }

        public override Task WriteAsync (
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            _ = buffer;
            _ = offset;
            _ = count;
            _ = cancellationToken;
            writeStarted.TrySetResult();
            return blockingStage == BlockingStage.Write
                ? writeCompletion.Task
                : Task.CompletedTask;
        }

        public override ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _ = buffer;
            _ = cancellationToken;
            writeStarted.TrySetResult();
            return blockingStage == BlockingStage.Write
                ? new ValueTask(writeCompletion.Task)
                : ValueTask.CompletedTask;
        }

        public void ReleaseBlockedOperations ()
        {
            writeCompletion.TrySetException(new IOException("late non-cooperative write failure"));
            readCompletion.TrySetResult(0);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                disposed.TrySetResult();
            }

            base.Dispose(disposing);
        }
    }
}
