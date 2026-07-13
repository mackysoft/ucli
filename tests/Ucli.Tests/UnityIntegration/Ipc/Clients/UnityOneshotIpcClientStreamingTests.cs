using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientStreamingTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendStreamingAsync_WhenSuccessful_UsesStreamingTransportAndForwardsProgress ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "streaming-success");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(
            request =>
            {
                return IpcRequestAssert.ParseMethod(request) switch
                {
                    UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                    UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                    UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                    _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
                };
            },
            request => new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                "test.progress",
                EmptyPayload(),
                null));
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());
        var progressFrames = new List<IpcStreamFrame>();

        var result = await client.SendStreamingAsync(
            unityProject,
            CreateDispatchRequest(IpcResponseMode.Stream),
            TimeSpan.FromSeconds(30),
            (frame, _) =>
            {
                progressFrames.Add(frame);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead);
        var dispatchRequest = IpcRequestAssert.SingleWithMethod(transportClient, UnityIpcMethod.OpsRead);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), dispatchRequest.ResponseMode);
        UnityIpcTransportClientAssert.SingleStreamingRequestSent(transportClient, UnityIpcMethod.OpsRead);
        IpcStreamFrameAssert.SingleEvent(progressFrames, "test.progress");
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendStreamingAsync_WhenProgressFrameHandlerFails_RethrowsHandlerExceptionAfterCleanup ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "streaming-progress-handler-failure");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var handlerException = new InvalidOperationException("progress frame rejected");
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new IpcProgressFrameHandlerException(handlerException),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendStreamingAsync(
                    unityProject,
                    CreateDispatchRequest(IpcResponseMode.Stream),
                    TimeSpan.FromSeconds(30),
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask();
        });

        Assert.Same(handlerException, exception);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead, UnityIpcMethod.Shutdown);
    }
}
