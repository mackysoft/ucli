using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc.Protocol;

public sealed class IpcStreamFrameWriterTests
{
    private static readonly TimeSpan TestFrameWriteTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WritesProgressFrameWithSerializedPayload ()
    {
        var request = CreateRequest("request-progress");
        await using var stream = new MemoryStream();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TestFrameWriteTimeout,
            writeFailureHandler: null);

        await writer.WriteProgressAsync(
            "test.progress",
            new TestProgressPayload("waiting", 3));

        stream.Position = 0;
        var frame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
            stream,
            IpcJsonSerializerOptions.Default);

        Assert.Equal(IpcProtocol.CurrentVersion, frame.ProtocolVersion);
        Assert.Equal("request-progress", frame.RequestId);
        Assert.Equal(IpcStreamFrameKinds.Progress, frame.Kind);
        Assert.Equal("test.progress", frame.Event);
        Assert.Null(frame.Response);
        Assert.Equal(JsonValueKind.Object, frame.Payload.ValueKind);
        Assert.Equal("waiting", frame.Payload.GetProperty("state").GetString());
        Assert.Equal(3, frame.Payload.GetProperty("step").GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteTerminalAsync_WritesTerminalFrameWithResponseAndEmptyPayload ()
    {
        var request = CreateRequest("request-terminal");
        var response = CreateResponse(request.RequestId);
        await using var stream = new MemoryStream();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TestFrameWriteTimeout,
            writeFailureHandler: null);

        await writer.WriteTerminalAsync(response);

        stream.Position = 0;
        var frame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
            stream,
            IpcJsonSerializerOptions.Default);

        Assert.Equal(IpcProtocol.CurrentVersion, frame.ProtocolVersion);
        Assert.Equal("request-terminal", frame.RequestId);
        Assert.Equal(IpcStreamFrameKinds.Terminal, frame.Kind);
        Assert.Null(frame.Event);
        Assert.Equal(JsonValueKind.Object, frame.Payload.ValueKind);
        Assert.Empty(frame.Payload.EnumerateObject());
        Assert.NotNull(frame.Response);
        Assert.Equal(IpcProtocol.StatusOk, frame.Response.Status);
        Assert.Equal("request-terminal", frame.Response.RequestId);
        Assert.Empty(frame.Response.Errors);
        Assert.Equal(JsonValueKind.Object, frame.Response.Payload.ValueKind);
        Assert.True(frame.Response.Payload.GetProperty("ok").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenConcurrentCallsShareStream_SerializesWrites ()
    {
        var request = CreateRequest("request-concurrent");
        await using var stream = new ConcurrentWriteDetectingStream();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TestFrameWriteTimeout,
            writeFailureHandler: null);
        var writeTasks = Enumerable
            .Range(0, 8)
            .Select(index => writer
                .WriteProgressAsync(
                    $"test.progress.{index}",
                    new TestProgressPayload("running", index))
                .AsTask())
            .ToArray();

        await Task.WhenAll(writeTasks);

        Assert.False(stream.HasOverlappingWrite);
        await using var outputStream = new MemoryStream(stream.ToArray());
        for (var index = 0; index < writeTasks.Length; index++)
        {
            var frame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
                outputStream,
                IpcJsonSerializerOptions.Default);

            Assert.Equal(IpcStreamFrameKinds.Progress, frame.Kind);
            Assert.Equal("request-concurrent", frame.RequestId);
        }

        Assert.Equal(outputStream.Length, outputStream.Position);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenConnectionLocalWriteFails_InvokesHandlerAndRethrowsOriginalException ()
    {
        var request = CreateRequest("request-write-failure");
        var expectedException = new IOException("write failed");
        await using var stream = new ThrowingWriteStream(expectedException);
        Exception? observedException = null;
        var handlerInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TestFrameWriteTimeout,
            exception =>
            {
                observedException = exception;
                handlerInvoked.TrySetResult();
            });

        var actualException = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await writer.WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("failed", 1));
        });

        Assert.Same(expectedException, actualException);
        await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(expectedException, observedException);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenCanceled_DoesNotInvokeWriteFailureHandler ()
    {
        var request = CreateRequest("request-canceled");
        await using var stream = new MemoryStream();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TestFrameWriteTimeout,
            static _ => throw new InvalidOperationException("Cancellation must not be reported as a write failure."));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await writer.WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("canceled", 1),
                cancellationTokenSource.Token);
        });

        Assert.Equal(0, stream.Length);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenFrameWriteDoesNotComplete_TimesOutAndRejectsSubsequentFrames ()
    {
        var request = CreateRequest("request-write-timeout");
        var response = CreateResponse(request.RequestId);
        await using var stream = new NonCooperativeWriteStream();
        Exception? observedException = null;
        var handlerInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(100),
            exception =>
            {
                observedException = exception;
                handlerInvoked.TrySetResult();
            });

        var writeTask = writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("blocked", 1))
            .AsTask();
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var timeoutException = await Assert.ThrowsAsync<IOException>(() => writeTask);
        var rejectedException = await Assert.ThrowsAsync<IOException>(async () =>
            await writer.WriteTerminalAsync(response));

        await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(timeoutException, observedException);
        Assert.Same(timeoutException, rejectedException);
        Assert.Equal(1, stream.WriteCount);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenTimedOutWriteCompletesLate_RemainsTimedOut ()
    {
        var request = CreateRequest("request-late-write-completion");
        var response = CreateResponse(request.RequestId);
        await using var stream = new NonCooperativeWriteStream();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(100),
            writeFailureHandler: null);
        var writeTask = writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("blocked", 1))
            .AsTask();
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var timeoutException = await Assert.ThrowsAsync<IOException>(() => writeTask);

        stream.CompleteWrite();
        await stream.SecondWriteStarted.WaitAsync(TimeSpan.FromSeconds(1));
        var rejectedException = await Assert.ThrowsAsync<IOException>(async () =>
            await writer.WriteTerminalAsync(response));

        Assert.Same(timeoutException, rejectedException);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenStreamBlocksBeforeReturningValueTask_TimesOutAndDisposesStream ()
    {
        var request = CreateRequest("request-synchronous-write-block");
        await using var stream = new SynchronousBlockingWriteStream();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(100),
            writeFailureHandler: null);

        var writeTask = Task.Run(async () => await writer.WriteProgressAsync(
            "test.progress",
            new TestProgressPayload("synchronously-blocked", 1)));
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            writeTask.WaitAsync(TimeSpan.FromSeconds(2)));

        await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains("Timed out while writing", exception.Message, StringComparison.Ordinal);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenWriteFailureHandlerBlocks_ReturnsAtFrameDeadline ()
    {
        var request = CreateRequest("request-blocking-write-failure-handler");
        await using var stream = new NonCooperativeWriteStream();
        var handlerStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(100),
            _ =>
            {
                handlerStarted.TrySetResult();
                releaseHandler.Task.GetAwaiter().GetResult();
            });

        var writeTask = writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("blocking-handler", 1))
            .AsTask();
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<IOException>(() => writeTask.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            releaseHandler.TrySetResult();
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenStreamDisposeBlocks_ReturnsAtFrameDeadline ()
    {
        var request = CreateRequest("request-blocking-stream-dispose");
        await using var stream = new BlockingDisposeWriteStream();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(100),
            writeFailureHandler: null);
        var writeTask = writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("blocking-dispose", 1))
            .AsTask();
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await stream.DisposeStarted.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<IOException>(() => writeTask.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            stream.AllowDispose();
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenConnectionLifetimeIsCanceledDuringUncancellableTransportWrite_ReleasesOwnedStream ()
    {
        var request = CreateRequest("request-active-write-canceled");
        await using var stream = new NonCooperativeWriteStream();
        Exception? observedException = null;
        using var connectionLifetimeCancellationTokenSource = new CancellationTokenSource();
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            connectionLifetimeCancellationTokenSource.Token,
            CancellationToken.None,
            TestFrameWriteTimeout,
            exception => observedException = exception);
        var writeTask = writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("blocked", 1),
                CancellationToken.None)
            .AsTask();
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        connectionLifetimeCancellationTokenSource.Cancel();
        connectionLifetimeCancellationTokenSource.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writeTask.WaitAsync(TimeSpan.FromSeconds(2)));
        await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Null(observedException);
        Assert.Equal(1, stream.WriteCount);
        Assert.False(stream.WriteCancellationCanBeCanceled);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenExecutionCancellationOccursAfterFrameStarts_CompletesFrameAndAllowsTerminalTimeoutResponse ()
    {
        var request = CreateRequest("request-partial-frame-canceled");
        var response = CreateTimeoutResponse(request.RequestId);
        using var executionCancellationTokenSource = new CancellationTokenSource();
        await using var stream = new CancelAfterFirstWriteStream(executionCancellationTokenSource);
        Exception? observedException = null;
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            TestFrameWriteTimeout,
            exception => observedException = exception);

        await writer.WriteProgressAsync(
            "test.progress",
            new TestProgressPayload("partially-written", 1),
            executionCancellationTokenSource.Token);
        await writer.WriteTerminalAsync(response, CancellationToken.None);

        Assert.True(executionCancellationTokenSource.IsCancellationRequested);
        Assert.Null(observedException);
        Assert.Equal(4, stream.WriteAttemptCount);
        Assert.False(stream.IsDisposed);

        stream.Position = 0;
        var progressFrame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
            stream,
            IpcJsonSerializerOptions.Default);
        var terminalFrame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
            stream,
            IpcJsonSerializerOptions.Default);

        Assert.Equal(IpcStreamFrameKinds.Progress, progressFrame.Kind);
        Assert.Equal(IpcStreamFrameKinds.Terminal, terminalFrame.Kind);
        Assert.NotNull(terminalFrame.Response);
        Assert.Equal(IpcProtocol.StatusError, terminalFrame.Response.Status);
        Assert.Equal(IpcTransportErrorCodes.IpcTimeout, Assert.Single(terminalFrame.Response.Errors).Code);
    }

    private static IpcRequest CreateRequest (string requestId)
    {
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            SessionToken: "session-token",
            Method: "test.method",
            Payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
            responseMode: IpcResponseMode.Stream);
    }

    private static IpcResponse CreateResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(new TestResponsePayload(true)),
            Errors: Array.Empty<IpcError>());
    }

    private static IpcResponse CreateTimeoutResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
            Errors: new[]
            {
                new IpcError(
                    IpcTransportErrorCodes.IpcTimeout,
                    "Execution timed out.",
                    null),
            });
    }

    private sealed record TestProgressPayload (
        string State,
        int Step);

    private sealed record TestResponsePayload (bool Ok);

    private sealed class NonCooperativeWriteStream : Stream
    {
        private readonly TaskCompletionSource writeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource writeCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource secondWriteStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int writeCount;

        private int isDisposed;

        private int writeCancellationCanBeCanceled;

        public Task WriteStarted => writeStarted.Task;

        public Task Disposed => disposed.Task;

        public Task SecondWriteStarted => secondWriteStarted.Task;

        public int WriteCount => Volatile.Read(ref writeCount);

        public bool WriteCancellationCanBeCanceled => Volatile.Read(ref writeCancellationCanBeCanceled) != 0;

        public bool IsDisposed => Volatile.Read(ref isDisposed) != 0;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public void CompleteWrite ()
        {
            writeCompletion.TrySetResult();
        }

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush ()
        {
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
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

        public override async ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Volatile.Write(
                ref writeCancellationCanBeCanceled,
                cancellationToken.CanBeCanceled ? 1 : 0);
            if (Interlocked.Increment(ref writeCount) >= 2)
            {
                secondWriteStarted.TrySetResult();
            }
            writeStarted.TrySetResult();
            await writeCompletion.Task.ConfigureAwait(false);
        }

        protected override void Dispose (bool disposing)
        {
            Volatile.Write(ref isDisposed, 1);
            disposed.TrySetResult();
            base.Dispose(disposing);
        }
    }

    private sealed class CancelAfterFirstWriteStream : MemoryStream
    {
        private readonly CancellationTokenSource cancellationTokenSource;

        private int writeAttemptCount;

        private int isDisposed;

        public CancelAfterFirstWriteStream (CancellationTokenSource cancellationTokenSource)
        {
            this.cancellationTokenSource = cancellationTokenSource;
        }

        public int WriteAttemptCount => Volatile.Read(ref writeAttemptCount);

        public bool IsDisposed => Volatile.Read(ref isDisposed) != 0;

        public override async ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref writeAttemptCount);
            cancellationToken.ThrowIfCancellationRequested();
            await base.WriteAsync(buffer, cancellationToken);
            if (WriteAttemptCount == 1)
            {
                cancellationTokenSource.Cancel();
            }
        }

        protected override void Dispose (bool disposing)
        {
            Volatile.Write(ref isDisposed, 1);
            base.Dispose(disposing);
        }
    }

    private sealed class SynchronousBlockingWriteStream : Stream
    {
        private readonly TaskCompletionSource writeStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource releaseWrite = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private int isDisposed;

        public Task WriteStarted => writeStarted.Task;

        public Task Disposed => disposed.Task;

        public bool IsDisposed => Volatile.Read(ref isDisposed) != 0;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush ()
        {
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
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

        public override ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            writeStarted.TrySetResult();
            releaseWrite.Task.GetAwaiter().GetResult();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose (bool disposing)
        {
            Volatile.Write(ref isDisposed, 1);
            disposed.TrySetResult();
            releaseWrite.TrySetResult();
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingDisposeWriteStream : Stream
    {
        private readonly TaskCompletionSource writeStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource writeCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposeStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource releaseDispose = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WriteStarted => writeStarted.Task;

        public Task DisposeStarted => disposeStarted.Task;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void AllowDispose ()
        {
            releaseDispose.TrySetResult();
        }

        public override void Flush ()
        {
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
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

        public override async ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            writeStarted.TrySetResult();
            await writeCompletion.Task.ConfigureAwait(false);
        }

        protected override void Dispose (bool disposing)
        {
            disposeStarted.TrySetResult();
            releaseDispose.Task.GetAwaiter().GetResult();
            base.Dispose(disposing);
        }
    }

}
