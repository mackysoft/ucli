using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc.Protocol;

public sealed class IpcStreamFrameWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WritesProgressFrameWithSerializedPayload ()
    {
        var request = CreateRequest();
        await using var stream = new MemoryStream();
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
            writeFailureHandler: null);

        await writer.WriteProgressAsync(
            "test.progress",
            new TestProgressPayload("waiting", 3));

        stream.Position = 0;
        var frame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
            stream,
            IpcJsonSerializerOptions.Default);

        Assert.Equal(IpcProtocol.CurrentVersion, frame.ProtocolVersion);
        Assert.Equal(request.RequestId, frame.RequestId);
        Assert.Equal(IpcStreamFrameKind.Progress, frame.Kind);
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
        var request = CreateRequest();
        var response = CreateResponse(request.RequestId);
        await using var stream = new MemoryStream();
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
            writeFailureHandler: null);

        await writer.WriteTerminalAsync(response);

        stream.Position = 0;
        var frame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
            stream,
            IpcJsonSerializerOptions.Default);

        Assert.Equal(IpcProtocol.CurrentVersion, frame.ProtocolVersion);
        Assert.Equal(request.RequestId, frame.RequestId);
        Assert.Equal(IpcStreamFrameKind.Terminal, frame.Kind);
        Assert.Null(frame.Event);
        Assert.Equal(JsonValueKind.Object, frame.Payload.ValueKind);
        Assert.Empty(frame.Payload.EnumerateObject());
        Assert.NotNull(frame.Response);
        Assert.Equal(IpcResponseStatus.Ok, frame.Response.Status);
        Assert.Equal(request.RequestId, frame.Response.RequestId);
        Assert.Empty(frame.Response.Errors);
        Assert.Equal(JsonValueKind.Object, frame.Response.Payload.ValueKind);
        Assert.True(frame.Response.Payload.GetProperty("ok").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Small")]
    public async Task WriteFrameAsync_AfterTerminalFrameWasWritten_RejectsEverySubsequentFrame (
        bool writeAnotherTerminalFrame)
    {
        var request = CreateRequest();
        await using var stream = new MemoryStream();
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
            writeFailureHandler: null);
        await writer.WriteTerminalAsync(CreateResponse(request.RequestId));

        var exception = writeAnotherTerminalFrame
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => writer
                .WriteTerminalAsync(CreateResponse(request.RequestId))
                .AsTask())
            : await Assert.ThrowsAsync<InvalidOperationException>(() => writer
                .WriteProgressAsync(
                    "test.late-progress",
                    new TestProgressPayload("late", 2))
                .AsTask());

        Assert.Contains("terminal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenConcurrentCallsShareStream_SerializesWrites ()
    {
        var request = CreateRequest();
        await using var stream = new ConcurrentWriteDetectingStream();
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
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

            Assert.Equal(IpcStreamFrameKind.Progress, frame.Kind);
            Assert.Equal(request.RequestId, frame.RequestId);
        }

        Assert.Equal(outputStream.Length, outputStream.Position);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenConnectionLocalWriteFails_InvokesHandlerAndRethrowsOriginalException ()
    {
        var request = CreateRequest();
        var expectedException = new IOException("write failed");
        await using var stream = new ThrowingWriteStream(expectedException);
        Exception? observedException = null;
        var handlerInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
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
        var request = CreateRequest();
        await using var stream = new MemoryStream();
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
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
        var request = CreateRequest();
        var response = CreateResponse(request.RequestId);
        await using var stream = new NonCooperativeWriteStream();
        using var frameWriteCutoffCancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Exception? observedException = null;
        var handlerInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            frameWriteCutoffCancellationTokenSource.Token,
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
    public async Task WriteTerminalAsync_WhenProgressHoldsWriteGate_StopsWaitingAtWriteCutoff ()
    {
        var request = CreateRequest();
        await using var stream = new NonCooperativeWriteStream();
        using var writeCutoffCancellationTokenSource = new CancellationTokenSource();
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            writeCutoffCancellationTokenSource.Token,
            writeFailureHandler: null);
        var progressWriteTask = writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("blocked", 1))
            .AsTask();
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var terminalWriteTask = writer
            .WriteTerminalAsync(CreateResponse(request.RequestId))
            .AsTask();

        writeCutoffCancellationTokenSource.Cancel();

        var terminalException = await Assert.ThrowsAsync<IOException>(() =>
            terminalWriteTask.WaitAsync(TimeSpan.FromSeconds(2)));
        var progressException = await Assert.ThrowsAsync<IOException>(() =>
            progressWriteTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Same(progressException, terminalException);
        Assert.Equal(1, stream.WriteCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenTimedOutWriteCompletesLate_RemainsTimedOut ()
    {
        var request = CreateRequest();
        var response = CreateResponse(request.RequestId);
        await using var stream = new NonCooperativeWriteStream();
        using var frameWriteCutoffCancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            frameWriteCutoffCancellationTokenSource.Token,
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
        var request = CreateRequest();
        await using var stream = new SynchronousBlockingWriteStream();
        using var frameWriteCutoffCancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            frameWriteCutoffCancellationTokenSource.Token,
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
        var request = CreateRequest();
        await using var stream = new NonCooperativeWriteStream();
        using var frameWriteCutoffCancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var handlerStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            frameWriteCutoffCancellationTokenSource.Token,
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
        var request = CreateRequest();
        await using var stream = new BlockingDisposeWriteStream();
        using var frameWriteCutoffCancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            frameWriteCutoffCancellationTokenSource.Token,
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
        var request = CreateRequest();
        await using var stream = new NonCooperativeWriteStream();
        Exception? observedException = null;
        using var connectionLifetimeCancellationTokenSource = new CancellationTokenSource();
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            connectionLifetimeCancellationTokenSource.Token,
            CancellationToken.None,
            CancellationToken.None,
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
    public async Task WriteProgressAsync_WhenWriteCutoffCancelsTransportWrite_RecordsTimeoutFailure ()
    {
        var request = CreateRequest();
        await using var stream = new CancellationCooperativeWriteStream();
        using var writeCutoffCancellationTokenSource = new CancellationTokenSource();
        Exception? observedException = null;
        var handlerInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            writeCutoffCancellationTokenSource.Token,
            writeCutoffCancellationTokenSource.Token,
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

        writeCutoffCancellationTokenSource.Cancel();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            writeTask.WaitAsync(TimeSpan.FromSeconds(2)));
        await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(exception, observedException);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenConnectionAndWriteCutoffCancelTogether_ClassifiesConnectionCancellationFirst ()
    {
        var request = CreateRequest();
        await using var stream = new CancellationCooperativeWriteStream();
        using var cancellationTokenSource = new CancellationTokenSource();
        Exception? observedException = null;
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            cancellationTokenSource.Token,
            cancellationTokenSource.Token,
            cancellationTokenSource.Token,
            exception => observedException = exception);
        var writeTask = writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("blocked", 1))
            .AsTask();
        await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writeTask.WaitAsync(TimeSpan.FromSeconds(2)));
        await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Null(observedException);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WhenExecutionCancellationOccursAfterFrameStarts_CompletesFrameAndAllowsTerminalTimeoutResponse ()
    {
        var request = CreateRequest();
        var response = CreateTimeoutResponse(request.RequestId);
        using var executionCancellationTokenSource = new CancellationTokenSource();
        await using var stream = new CancelAfterFirstWriteStream(executionCancellationTokenSource);
        Exception? observedException = null;
        using var writer = new IpcStreamFrameWriter(
            stream,
            request,
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
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

        Assert.Equal(IpcStreamFrameKind.Progress, progressFrame.Kind);
        Assert.Equal(IpcStreamFrameKind.Terminal, terminalFrame.Kind);
        Assert.NotNull(terminalFrame.Response);
        Assert.Equal(IpcResponseStatus.Error, terminalFrame.Response.Status);
        Assert.Equal(IpcTransportErrorCodes.IpcTimeout, Assert.Single(terminalFrame.Response.Errors).Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Dispose_ReleasesWriterAndRejectsSubsequentFrames ()
    {
        await using var stream = new MemoryStream();
        using var writer = new IpcStreamFrameWriter(
            stream,
            CreateRequest(),
            CancellationToken.None,
            CancellationToken.None,
            CancellationToken.None,
            writeFailureHandler: null);

        writer.Dispose();
        writer.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => writer
            .WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("disposed", 1))
            .AsTask());
    }

    private static IpcRequestEnvelope CreateRequest ()
    {
        return new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: "session-token",
            method: "test.method",
            payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
            responseMode: "stream",
            requestDeadlineUtc: DateTimeOffset.MaxValue,
            requestDeadlineRemainingMilliseconds: int.MaxValue);
    }

    private static IpcResponse CreateResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement(new TestResponsePayload(true)),
            errors: Array.Empty<IpcError>());
    }

    private static IpcResponse CreateTimeoutResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
            errors: new[]
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

    private sealed class CancellationCooperativeWriteStream : Stream
    {
        private readonly TaskCompletionSource writeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public override async ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            writeStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
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
