using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc.Protocol;

public sealed class IpcStreamFrameWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteProgressAsync_WritesProgressFrameWithSerializedPayload ()
    {
        var request = CreateRequest("request-progress");
        await using var stream = new MemoryStream();
        var writer = new IpcStreamFrameWriter(stream, request);

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
        var writer = new IpcStreamFrameWriter(stream, request);

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
        var writer = new IpcStreamFrameWriter(stream, request);
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
        var writer = new IpcStreamFrameWriter(
            stream,
            request,
            exception => observedException = exception);

        var actualException = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await writer.WriteProgressAsync(
                "test.progress",
                new TestProgressPayload("failed", 1));
        });

        Assert.Same(expectedException, actualException);
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

    private sealed record TestProgressPayload (
        string State,
        int Step);

    private sealed record TestResponsePayload (bool Ok);

}
