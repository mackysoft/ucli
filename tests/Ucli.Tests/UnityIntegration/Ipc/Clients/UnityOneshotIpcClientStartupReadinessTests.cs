using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
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
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId, projectFingerprint: ProjectFingerprintTestFactory.Create("other-project-fingerprint")),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
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
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Shutdown);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(IpcEditorLifecycleStateCodec.Starting)]
    public async Task SendAsync_WhenStartupPingReportsWaitableState_RetriesUntilReadyBeforeSendingRequest (
        string lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", $"startup-retry-{lifecycleState}");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: ++pingAttempt == 1 ? lifecycleState : IpcEditorLifecycleStateCodec.Ready,
                    canAcceptExecutionRequests: pingAttempt != 1),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
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
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(IpcEditorLifecycleStateCodec.CompileFailed)]
    [InlineData(IpcEditorLifecycleStateCodec.SafeMode)]
    public async Task SendAsync_WhenStartupPingReportsAllowedLifecycleState_DispatchesRequestWithoutReadiness (
        string lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", $"startup-allowed-{lifecycleState}");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: lifecycleState,
                    canAcceptExecutionRequests: false),
                UnityIpcMethod.Compile => CreateSuccessResponse(request.RequestId),
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
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Compile);
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
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: IpcEditorLifecycleStateCodec.Starting,
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
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Shutdown);
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
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => HandlePing(request),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
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
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Ping, UnityIpcMethod.Shutdown);
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
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => HandleStartupPing(request),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
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
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Shutdown);
        var startupPingRequest = IpcRequestAssert.SingleWithMethod(transportClient, UnityIpcMethod.Ping);
        Assert.True(IpcPayloadCodec.TryDeserialize(startupPingRequest.Payload, out IpcPingRequest startupPing, out _));
        Assert.Equal(IpcPingClientVersions.OneshotStartup, startupPing.ClientVersion);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);

        static IpcResponse HandleStartupPing (IpcRequest request)
        {
            Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
            Assert.Equal(IpcPingClientVersions.OneshotStartup, payload.ClientVersion);
            return CreatePingResponse(
                request.RequestId,
                lifecycleState: IpcEditorLifecycleStateCodec.Starting,
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
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping when ++pingAttempt == 1 => throw new TimeoutException("startup ping timed out"),
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
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
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.Ping,
            UnityIpcMethod.Ping,
            UnityIpcMethod.OpsRead);
        var startupProbeRequests = IpcRequestAssert.WithMethod(requests, UnityIpcMethod.Ping);
        Assert.NotEqual(Guid.Empty, IpcRequestAssert.SingleRequestId(startupProbeRequests));
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupPingResponseReadIsInterrupted_RetriesUntilReachable ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-response-read-interrupted");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping when ++pingAttempt == 1 => throw new IpcResponseReadInterruptedException(
                    new IOException("Pipe is broken.")),
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
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
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.Ping,
            UnityIpcMethod.Ping,
            UnityIpcMethod.OpsRead);
        var startupProbeRequests = IpcRequestAssert.WithMethod(requests, UnityIpcMethod.Ping);
        Assert.NotEqual(Guid.Empty, IpcRequestAssert.SingleRequestId(startupProbeRequests));
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
    }
}
