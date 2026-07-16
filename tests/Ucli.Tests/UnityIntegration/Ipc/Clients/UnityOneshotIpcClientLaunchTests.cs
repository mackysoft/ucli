using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
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
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var lockProvider = new StubProjectLifecycleLockProvider();
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var client = CreateClient(
            launcher,
            transportClient,
            lockProvider,
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        ProjectLifecycleLockProviderAssert.AcquiredOnceFor(lockProvider, unityProject);
        var bootstrapArguments = UnityOneshotLaunchAssert.LaunchedOnceWithDefaultOptions(launcher, unityProject);
        var requests = IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead);
        var dispatchRequest = IpcRequestAssert.SingleWithMethod(requests, UnityIpcMethod.OpsRead);
        Assert.Equal(CreateDispatchPayload().GetRawText(), dispatchRequest.Payload.GetRawText());
        Assert.All(transportClient.UnityInvocations, invocation =>
        {
            Assert.Equal(startedAtUtc + TimeSpan.FromSeconds(30), invocation.Request.RequestDeadlineUtc);
        });
        IpcRequestAssert.AllSessionToken(requests, bootstrapArguments.SessionToken.GetEncodedValue());
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
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.BuildRun => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            new UnityIpcDispatchRequest(
                UnityIpcMethod.BuildRun,
                CreateDispatchPayload(),
                new UnityBatchmodeLaunchOptions(new UnityBuildProfileAssetPath(
                    "Assets/BuildProfiles/LinuxPlayer.asset"))),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
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
        var client = CreateClient(
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
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
    }

}
