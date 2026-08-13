using System.Buffers.Binary;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class IpcTransportTestHarness
{
    internal static async Task WithUnixStreamingServerAsync (
        Func<IpcRequestEnvelope, Stream, CancellationToken, Task> writeFramesAsync,
        Func<IpcTransportEndpoint, IpcRequestEnvelope, Task> executeClientAsync,
        TimeSpan waitTimeout)
    {
        var endpoint = IpcTransportEndpoint.FromUnixSocketPath(
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath);
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromUnixSocketPath(endpoint.UnixSocketPath!),
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
                if (!readResult.IsSuccess)
                {
                    throw new InvalidDataException(readResult.ErrorMessage);
                }

                await writeFramesAsync(readResult.Value, stream, cancellationToken);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            cancellationTokenSource.Token);

        try
        {
            await startedTaskSource.Task.WaitAsync(waitTimeout);
            await executeClientAsync(endpoint, CreateStreamingRequest());
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await serverTask.WaitAsync(waitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    internal static async Task WithUnixResponseServerAsync (
        Func<IpcRequestEnvelope, Stream, CancellationToken, Task> writeResponseAsync,
        Func<IpcTransportEndpoint, IpcRequestEnvelope, Task> executeClientAsync,
        TimeSpan waitTimeout)
    {
        var endpoint = IpcTransportEndpoint.FromUnixSocketPath(
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath);
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromUnixSocketPath(endpoint.UnixSocketPath!),
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
                if (!readResult.IsSuccess)
                {
                    throw new InvalidDataException(readResult.ErrorMessage);
                }

                await writeResponseAsync(readResult.Value, stream, cancellationToken);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            cancellationTokenSource.Token);

        try
        {
            await startedTaskSource.Task.WaitAsync(waitTimeout);
            await executeClientAsync(endpoint, CreateSingleRequest());
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await serverTask.WaitAsync(waitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    internal static async Task WithUnixResponseServerCapturingRequestPayloadAsync (
        Func<ReadOnlyMemory<byte>, IpcRequestEnvelope, Stream, CancellationToken, Task> writeResponseAsync,
        Func<IpcTransportEndpoint, IpcRequestEnvelope, Task> executeClientAsync,
        TimeSpan waitTimeout)
    {
        var endpoint = IpcTransportEndpoint.FromUnixSocketPath(
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath);
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromUnixSocketPath(endpoint.UnixSocketPath!),
            async (stream, cancellationToken) =>
            {
                var requestPayload = await ReadRequestPayloadAsync(stream, cancellationToken);
                var request = JsonSerializer.Deserialize<IpcRequestEnvelope>(
                    requestPayload.Span,
                    IpcJsonSerializerOptions.Default);
                if (request is null)
                {
                    throw new InvalidDataException("IPC request payload could not be deserialized.");
                }

                await writeResponseAsync(requestPayload, request, stream, cancellationToken);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            cancellationTokenSource.Token);

        try
        {
            await startedTaskSource.Task.WaitAsync(waitTimeout);
            await executeClientAsync(endpoint, CreateSingleRequest());
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await serverTask.WaitAsync(waitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    internal static async Task WriteStreamFrameAsync (
        Stream stream,
        IpcStreamFrame frame,
        CancellationToken cancellationToken)
    {
        await IpcFrameCodec.WriteModelAsync(
            stream,
            frame,
            IpcJsonSerializerOptions.Default,
            cancellationToken: cancellationToken);
    }

    internal static IpcRequestEnvelope CreateStreamingRequest ()
    {
        return new IpcRequestEnvelope(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            "token",
            TextVocabulary.GetText(UnityIpcMethod.Ping),
            Json("{}"),
            TextVocabulary.GetText(IpcResponseMode.Stream),
            DateTimeOffset.MaxValue,
            int.MaxValue);
    }

    internal static IpcRequestEnvelope CreateSingleRequest ()
    {
        return new IpcRequestEnvelope(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            "token",
            TextVocabulary.GetText(UnityIpcMethod.Ping),
            Json("{}"),
            TextVocabulary.GetText(IpcResponseMode.Single),
            DateTimeOffset.MaxValue,
            int.MaxValue);
    }

    internal static IpcResponse CreateResponse (
        Guid requestId,
        string payloadJson,
        int? protocolVersion = null,
        IpcResponseStatus? status = null)
    {
        return new IpcResponse(
            protocolVersion ?? IpcProtocol.CurrentVersion,
            requestId,
            status ?? IpcResponseStatus.Ok,
            Json(payloadJson),
            Array.Empty<IpcError>());
    }

    internal static JsonElement Json (string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async ValueTask<ReadOnlyMemory<byte>> ReadRequestPayloadAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength < 0 || payloadLength > IpcFrameCodec.DefaultMaxFrameSizeInBytes)
        {
            throw new InvalidDataException("IPC request frame length is invalid.");
        }

        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return payload;
    }

    private static async ValueTask ReadExactlyAsync (
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var readLength = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (readLength == 0)
            {
                throw new EndOfStreamException("IPC stream ended before a complete request frame was read.");
            }

            offset += readLength;
        }
    }
}
