using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientStartupReadinessTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupPingProjectFingerprintMismatches_ReturnsFailureWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-fingerprint-mismatch");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId, projectFingerprint: "other-project-fingerprint"),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
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
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("projectFingerprint mismatch", result.Message, StringComparison.Ordinal);
        IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.Shutdown);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(IpcEditorLifecycleState.Starting)]
    public async Task SendAsync_WhenStartupPingReportsWaitableState_RetriesUntilReadyBeforeSendingRequest (
        IpcEditorLifecycleState lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            $"startup-retry-{ContractLiteralCodec.ToValue(lifecycleState)}");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: ++pingAttempt == 1 ? lifecycleState : IpcEditorLifecycleState.Ready,
                    canAcceptExecutionRequests: pingAttempt != 1),
                IpcMethodNames.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var resultTask = client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None).AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(30),
            MaximumStartupRetryDelay);
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.Ping, IpcMethodNames.OpsRead);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(IpcEditorLifecycleState.CompileFailed)]
    [InlineData(IpcEditorLifecycleState.SafeMode)]
    public async Task SendAsync_WhenStartupPingReportsAllowedLifecycleState_DispatchesRequestWithoutReadiness (
        IpcEditorLifecycleState lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            $"startup-allowed-{ContractLiteralCodec.ToValue(lifecycleState)}");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: lifecycleState,
                    canAcceptExecutionRequests: false),
                IpcMethodNames.Compile => CreateSuccessResponse(request.RequestId),
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
            CreateCompileDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.Compile);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupPingReportsWaitableStateAndRequestIsFailFast_ReturnsLifecycleFailureWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-fail-fast");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: IpcEditorLifecycleState.Starting,
                    canAcceptExecutionRequests: false),
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
            CreateOpsReadDispatchRequest(failFast: true, requireReadinessGate: true),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorStarting, result.ErrorCode);
        IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.Shutdown);
        UnityBatchmodeProcessHandleAssert.TerminatedOnce(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenReadyPingDispatchSucceeds_SendsShutdownBeforeWaitingForExit ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "ready-ping-shutdown");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => HandlePing(request),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
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
            CreateReadyPingDispatchRequest(failFast: false),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.Ping, IpcMethodNames.Shutdown);
        AssertCleanupShutdownUsesLaunchSession(launcher, transportClient, unityProject);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);

        static IpcResponse HandlePing (IpcRequest request)
        {
            Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
            return payload.ClientVersion switch
            {
                IpcPingClientVersions.OneshotStartup => CreatePingResponse(request.RequestId),
                IpcPingClientVersions.Ready => CreatePingResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected ping client: {payload.ClientVersion}"),
            };
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenReadyPingRequestIsFailFast_UsesFailFastStartupProbeWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "ready-ping-startup-fail-fast");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => HandleStartupPing(request),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
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
            CreateReadyPingDispatchRequest(failFast: true),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorStarting, result.ErrorCode);
        IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.Shutdown);
        var startupPingRequest = IpcRequestAssert.SingleWithMethod(transportClient, IpcMethodNames.Ping);
        Assert.True(IpcPayloadCodec.TryDeserialize(startupPingRequest.Payload, out IpcPingRequest startupPing, out _));
        Assert.Equal(IpcPingClientVersions.OneshotStartup, startupPing.ClientVersion);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);

        static IpcResponse HandleStartupPing (IpcRequest request)
        {
            Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
            Assert.Equal(IpcPingClientVersions.OneshotStartup, payload.ClientVersion);
            return CreatePingResponse(
                request.RequestId,
                lifecycleState: IpcEditorLifecycleState.Starting,
                canAcceptExecutionRequests: false);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupPingTimesOut_RetriesUntilReachable ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-timeout-retry");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping when ++pingAttempt == 1 => throw new TimeoutException("startup ping timed out"),
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var resultTask = client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None).AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(30),
            MaximumStartupRetryDelay);
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        IpcRequestAssert.Methods(transportClient, IpcMethodNames.Ping, IpcMethodNames.Ping, IpcMethodNames.OpsRead);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
    }
}
