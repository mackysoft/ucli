using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorTransportServerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenOneConnectionBlocks_StillAcceptsAnotherConnection ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "parallel-accept");
        var endpoint = CreateEndpoint(scope.FullPath);
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowRequestEnteredTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlowRequestTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.Run(
            endpoint,
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                        stream,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                Assert.True(readResult.IsSuccess);

                var request = readResult.Value;
                if (request.Method == "slow")
                {
                    slowRequestEnteredTaskSource.TrySetResult();
                    await releaseSlowRequestTaskSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                var response = new IpcResponse(
                    ProtocolVersion: request.ProtocolVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: IpcPayloadCodec.SerializeToElement(new TransportServerResponse(request.Method)),
                    Errors: Array.Empty<IpcError>());
                await IpcFrameCodec.WriteModelAsync(
                        stream,
                        response,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        try
        {
            await startedTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var client = new IpcTransportClient();
            var slowRequestTask = client.SendAsync(
                    endpoint,
                    CreateRequest("slow"),
                    TimeSpan.FromSeconds(5))
                .AsTask();

            await slowRequestEnteredTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var fastRequestTask = client.SendAsync(
                    endpoint,
                    CreateRequest("fast"),
                    TimeSpan.FromSeconds(5))
                .AsTask();
            var completedTask = await Task.WhenAny(fastRequestTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(fastRequestTask, completedTask);

            var fastResponse = await fastRequestTask;
            Assert.True(IpcPayloadCodec.TryDeserialize(
                fastResponse.Payload,
                out TransportServerResponse fastPayload,
                out _));
            Assert.Equal("fast", fastPayload.Method);

            releaseSlowRequestTaskSource.TrySetResult();

            var slowResponse = await slowRequestTask;
            Assert.True(IpcPayloadCodec.TryDeserialize(
                slowResponse.Payload,
                out TransportServerResponse slowPayload,
                out _));
            Assert.Equal("slow", slowPayload.Method);
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await serverTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static IpcEndpoint CreateEndpoint (string storageRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            return new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-supervisor-transport-{Guid.NewGuid():N}");
        }

        return new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            $"/tmp/ucli-supervisor-transport-{Guid.NewGuid():N}.sock");
    }

    private static IpcRequest CreateRequest (string method)
    {
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"request-{Guid.NewGuid():N}",
            SessionToken: "session-token",
            Method: method,
            Payload: IpcPayloadCodec.SerializeToElement(new { }));
    }

    private sealed record TransportServerResponse (string Method);
}