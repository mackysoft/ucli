using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class IpcTransportTestHarness
{
    internal static async Task WithUnixStreamingServerAsync (
        Func<IpcRequest, Stream, CancellationToken, Task> writeFramesAsync,
        Func<IpcEndpoint, IpcRequest, Task> executeClientAsync,
        TimeSpan waitTimeout)
    {
        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath("ucli-supervisor-", Guid.NewGuid().ToString("N")));
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
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
        Func<IpcRequest, Stream, CancellationToken, Task> writeResponseAsync,
        Func<IpcEndpoint, IpcRequest, Task> executeClientAsync,
        TimeSpan waitTimeout)
    {
        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath("ucli-supervisor-", Guid.NewGuid().ToString("N")));
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
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

    internal static IpcRequest CreateStreamingRequest ()
    {
        return new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            Json("{}"),
            IpcResponseMode.Stream);
    }

    internal static IpcRequest CreateSingleRequest ()
    {
        return new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            Json("{}"),
            IpcResponseMode.Single);
    }

    internal static IpcResponse CreateResponse (
        string requestId,
        string payloadJson,
        int? protocolVersion = null,
        string? status = null)
    {
        return new IpcResponse(
            protocolVersion ?? IpcProtocol.CurrentVersion,
            requestId,
            status ?? IpcProtocol.StatusOk,
            Json(payloadJson),
            Array.Empty<IpcError>());
    }

    internal static JsonElement Json (string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
