using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientLaunchTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenSuccessful_AcquiresProjectLockAndUsesConfiguredEndpoint ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "success");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var lockProvider = new StubProjectLifecycleLockProvider();
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            lockProvider,
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        ProjectLifecycleLockProviderAssert.AcquiredOnceFor(lockProvider, unityProject);
        var bootstrapArguments = UnityOneshotLaunchAssert.LaunchedOnceWithDefaultOptions(launcher, unityProject);
        var requests = IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.OpsRead);
        var dispatchRequest = IpcRequestAssert.SingleWithMethod(requests, IpcMethodNames.OpsRead);
        Assert.Equal(CreateDispatchPayload().GetRawText(), dispatchRequest.Payload.GetRawText());
        IpcRequestAssert.AllSessionToken(requests, bootstrapArguments.SessionToken);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WithOneshotActiveBuildProfilePath_PassesLaunchOptions ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "active-build-profile");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            new UnityIpcDispatchRequest(
                IpcMethodNames.OpsRead,
                CreateDispatchPayload(),
                oneshotActiveBuildProfilePath: "Assets/BuildProfiles/LinuxPlayer.asset"),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        UnityOneshotLaunchAssert.LaunchedOnceWithActiveBuildProfile(
            launcher,
            unityProject,
            "Assets/BuildProfiles/LinuxPlayer.asset");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenLifecycleLockAcquisitionTimesOut_ReturnsIpcTimeoutWithoutLaunchingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "lock-timeout");
        var launcher = new UnexpectedUnityBatchmodeProcessLauncher("Lifecycle lock timeout should not launch Unity.");
        var client = new UnityOneshotIpcClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => CreateSuccessResponse(Guid.NewGuid())),
            new StubProjectLifecycleLockProvider((_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Timed out while waiting for lifecycle lock.");
            }),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
    }

}
