using System.Text.Json;
using MackySoft.FileSystem;
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
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "IPC streaming transport server start", waitTimeout);
            await executeClientAsync(endpoint, CreateStreamingRequest());
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await TestAwaiter.WaitAsync(serverTask, "IPC streaming transport server shutdown", waitTimeout);
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
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "IPC transport server start", waitTimeout);
            await executeClientAsync(endpoint, CreateSingleRequest());
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await TestAwaiter.WaitAsync(serverTask, "IPC transport server shutdown", waitTimeout);
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
            ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
            Json("{}"),
            ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
            DateTimeOffset.MaxValue,
            int.MaxValue);
    }

    internal static IpcRequestEnvelope CreateSingleRequest ()
    {
        return new IpcRequestEnvelope(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            "token",
            ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
            Json("{}"),
            ContractLiteralCodec.ToValue(IpcResponseMode.Single),
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
}
