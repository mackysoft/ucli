using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientStartupReadinessTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartAdmissionPolicyWaits_WaitsBeforeStartAndUsesGraceOnlyAfterStartWrite ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "start-admission-wait");
        var startedAtUtc = DateTimeOffset.UnixEpoch;
        var timeProvider = new FakeTimeProvider(startedAtUtc);
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var firstPingCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: GetLifecycleState()),
                UnityIpcMethod.Refresh =>
                    CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException(
                    $"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);
        var dispatchRequest = CreateRefreshDispatchRequest(
            failFast: false,
            timeProvider: timeProvider,
            executionTimeout: TimeSpan.FromSeconds(30));

        var resultTask = client.SendAsync(
            unityProject,
            dispatchRequest,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                timeProvider),
            CancellationToken.None).AsTask();
        await firstPingCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(MaximumStartupRetryDelay);
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.LifecycleExecutionStart);
        Assert.True(result.LifecycleActionDispatched);
        var requests = transportClient.Requests;
        Assert.Equal(
            2,
            requests.Count(request =>
                IpcRequestAssert.ParseMethod(request)
                == UnityIpcMethod.Ping));
        var lifecycleStart = IpcRequestAssert.SingleWithMethod(
            transportClient,
            UnityIpcMethod.LifecycleStart);
        var action = IpcRequestAssert.SingleWithMethod(
            transportClient,
            UnityIpcMethod.Refresh);
        Assert.Equal(
            startedAtUtc.AddSeconds(33),
            lifecycleStart.RequestDeadlineUtc);
        Assert.Equal(
            startedAtUtc.AddSeconds(33),
            action.RequestDeadlineUtc);
        var bootstrap = UnityOneshotLaunchAssert.LaunchedOnce(
            launcher,
            unityProject,
            startedAtUtc);
        Assert.Equal(
            startedAtUtc.AddSeconds(33),
            bootstrap.ExitDeadlineUtc);

        UnityEditorLifecycleState GetLifecycleState ()
        {
            if (++pingAttempt == 1)
            {
                firstPingCompleted.TrySetResult();
                return UnityEditorLifecycleState.Busy;
            }

            return UnityEditorLifecycleState.Ready;
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartAdmissionPolicyRetriesRejectedStart_ReprobesWithSameExecutionAndFreshRequestId ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "refresh-late-busy");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var startRequests = new List<IpcRequestEnvelope>();
        var transportClient = new RecordingUnityIpcTransportClient(
            request =>
            {
                switch (IpcRequestAssert.ParseMethod(request))
                {
                    case UnityIpcMethod.Ping:
                        return CreatePingResponse(request.RequestId);
                    case UnityIpcMethod.LifecycleStart:
                        startRequests.Add(request);
                        return startRequests.Count == 1
                            ? CreateErrorResponse(
                                request.RequestId,
                                EditorLifecycleErrorCodes.EditorBusy,
                                "Unity editor became busy before Start persistence.")
                            : LifecycleExecutionIpcTestResponseFactory
                                .TryCreateResponse(request)!;
                    case UnityIpcMethod.Refresh:
                        return CreateSuccessResponse(request.RequestId);
                    default:
                        throw new Xunit.Sdk.XunitException(
                            $"Unexpected method: {request.Method}");
                }
            },
            createLifecycleStartResponses: false);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());
        var dispatchRequest =
            CreateRefreshDispatchRequest(failFast: false);

        var result = await client.SendAsync(
            unityProject,
            dispatchRequest,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, startRequests.Count);
        Assert.NotEqual(
            startRequests[0].RequestId,
            startRequests[1].RequestId);
        var firstStart = ReadStartRequest(startRequests[0]);
        var secondStart = ReadStartRequest(startRequests[1]);
        Assert.Equal(firstStart.ExecutionId, secondStart.ExecutionId);
        Assert.Equal(
            firstStart.DefinitionDigest,
            secondStart.DefinitionDigest);
        Assert.Equal(
            dispatchRequest.Registration!.ExecutionId,
            firstStart.ExecutionId);
        Assert.Equal(
            2,
            transportClient.Requests.Count(request =>
                IpcRequestAssert.ParseMethod(request)
                == UnityIpcMethod.Ping));
        Assert.Single(
            transportClient.Requests,
            request => IpcRequestAssert.ParseMethod(request)
                == UnityIpcMethod.Refresh);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartAdmissionDeadlineExpires_DoesNotUseHardExitGraceToStart ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "refresh-start-deadline");
        var startedAtUtc = DateTimeOffset.UnixEpoch;
        var timeProvider = new FakeTimeProvider(startedAtUtc);
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var startupPingCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreateBusyPingResponse(request),
                UnityIpcMethod.Shutdown =>
                    CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException(
                    $"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);
        var startAdmissionTimeout = TimeSpan.FromMilliseconds(100);

        var resultTask = client.SendAsync(
            unityProject,
            CreateRefreshDispatchRequest(
                failFast: false,
                timeProvider: timeProvider,
                executionTimeout: startAdmissionTimeout),
            ExecutionDeadline.Start(
                startAdmissionTimeout,
                timeProvider),
            CancellationToken.None).AsTask();
        await startupPingCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(
            startAdmissionTimeout + LifecycleExecutionTiming.ResponseDeliveryGrace);
        var result = await resultTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Null(result.LifecycleExecutionStart);
        Assert.False(result.LifecycleActionDispatched);
        Assert.DoesNotContain(
            transportClient.Requests,
            request => IpcRequestAssert.ParseMethod(request)
                is UnityIpcMethod.LifecycleStart or UnityIpcMethod.Refresh);
        var bootstrap = UnityOneshotLaunchAssert.LaunchedOnce(
            launcher,
            unityProject,
            startedAtUtc);
        Assert.Equal(
            startedAtUtc
            + startAdmissionTimeout
            + LifecycleExecutionTiming.ResponseDeliveryGrace,
            bootstrap.ExitDeadlineUtc);

        IpcResponse CreateBusyPingResponse (IpcRequestEnvelope request)
        {
            startupPingCompleted.TrySetResult();
            return CreatePingResponse(
                request.RequestId,
                lifecycleState: UnityEditorLifecycleState.Busy);
        }
    }

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
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("projectFingerprint mismatch", result.Message, StringComparison.Ordinal);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Shutdown);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UnityEditorLifecycleState.Starting)]
    public async Task SendAsync_WhenStartupPingReportsWaitableState_RetriesUntilReadyBeforeSendingRequest (
        UnityEditorLifecycleState lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            $"startup-retry-{TextVocabulary.GetText(lifecycleState)}");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var firstPingCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: GetLifecycleState()),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var resultTask = client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None).AsTask();
        await firstPingCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(MaximumStartupRetryDelay);
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.Ping,
            UnityIpcMethod.Ping,
            UnityIpcMethod.OpsRead);
        var startupProbeRequests = IpcRequestAssert.WithMethod(requests, UnityIpcMethod.Ping);
        _ = IpcRequestAssert.SingleRequestId(startupProbeRequests);
        Assert.True(
            startupProbeRequests[1].RequestDeadlineRemainingMilliseconds
            < startupProbeRequests[0].RequestDeadlineRemainingMilliseconds);

        UnityEditorLifecycleState GetLifecycleState ()
        {
            if (++pingAttempt == 1)
            {
                firstPingCompleted.TrySetResult();
                return lifecycleState;
            }

            return UnityEditorLifecycleState.Ready;
        }
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UnityEditorLifecycleState.CompileFailed)]
    [InlineData(UnityEditorLifecycleState.SafeMode)]
    public async Task SendAsync_WhenStartupPingReportsAllowedLifecycleState_DispatchesRequestWithoutReadiness (
        UnityEditorLifecycleState lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            $"startup-allowed-{TextVocabulary.GetText(lifecycleState)}");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: lifecycleState),
                UnityIpcMethod.Compile => CreateSuccessResponse(request.RequestId),
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
            CreateCompileDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.True(
            result.IsSuccess,
            result.Message
            + " Requests: "
            + string.Join(", ", transportClient.Requests.Select(static request => request.Method)));
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
                    lifecycleState: UnityEditorLifecycleState.Starting),
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
            CreateOpsReadDispatchRequest(failFast: true, requireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
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
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateReadyPingDispatchRequest(failFast: false),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Ping, UnityIpcMethod.Shutdown);
        AssertCleanupShutdownUsesLaunchSession(launcher, transportClient, unityProject);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);

        static IpcResponse HandlePing (IpcRequestEnvelope request)
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
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateReadyPingDispatchRequest(failFast: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorStarting, result.ErrorCode);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.Shutdown);
        var startupPingRequest = IpcRequestAssert.SingleWithMethod(transportClient, UnityIpcMethod.Ping);
        Assert.True(IpcPayloadCodec.TryDeserialize(startupPingRequest.Payload, out IpcPingRequest startupPing, out _));
        Assert.Equal(IpcPingClientVersions.OneshotStartup, startupPing.ClientVersion);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);

        static IpcResponse HandleStartupPing (IpcRequestEnvelope request)
        {
            Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
            Assert.Equal(IpcPingClientVersions.OneshotStartup, payload.ClientVersion);
            return CreatePingResponse(
                request.RequestId,
                lifecycleState: UnityEditorLifecycleState.Starting);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupPingTimesOut_RetriesUntilReachable ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-timeout-retry");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var firstPingCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping when ++pingAttempt == 1 => ThrowStartupPingTimeout(),
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var resultTask = client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None).AsTask();
        await firstPingCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(MaximumStartupRetryDelay);
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.Ping,
            UnityIpcMethod.Ping,
            UnityIpcMethod.OpsRead);
        var startupProbeRequests = IpcRequestAssert.WithMethod(requests, UnityIpcMethod.Ping);
        Assert.NotEqual(Guid.Empty, IpcRequestAssert.SingleRequestId(startupProbeRequests));
        Assert.True(
            startupProbeRequests[1].RequestDeadlineRemainingMilliseconds
            < startupProbeRequests[0].RequestDeadlineRemainingMilliseconds);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);

        IpcResponse ThrowStartupPingTimeout ()
        {
            firstPingCompleted.TrySetResult();
            throw new TimeoutException("startup ping timed out");
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupPingConnectionFailsBeforeRequestWrite_RetriesUntilReachable ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-connect-retry");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var firstPingCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping when ++pingAttempt == 1 => ThrowStartupConnectFailure(),
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var resultTask = client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None).AsTask();
        await firstPingCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(MaximumStartupRetryDelay);
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.Ping,
            UnityIpcMethod.Ping,
            UnityIpcMethod.OpsRead);
        var startupProbeRequests = IpcRequestAssert.WithMethod(requests, UnityIpcMethod.Ping);
        Assert.NotEqual(Guid.Empty, IpcRequestAssert.SingleRequestId(startupProbeRequests));
        Assert.True(
            startupProbeRequests[1].RequestDeadlineRemainingMilliseconds
            < startupProbeRequests[0].RequestDeadlineRemainingMilliseconds);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);

        IpcResponse ThrowStartupConnectFailure ()
        {
            firstPingCompleted.TrySetResult();
            throw new IpcConnectException(
                "IPC connection failed before the request was sent.",
                new IOException("Named pipe connection failed."));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupPingResponseReadIsInterrupted_RetriesUntilReachable ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-response-read-interrupted");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var firstPingCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping when ++pingAttempt == 1 => ThrowStartupResponseInterruption(),
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var resultTask = client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None).AsTask();
        await firstPingCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(MaximumStartupRetryDelay);
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.Ping,
            UnityIpcMethod.Ping,
            UnityIpcMethod.OpsRead);
        var startupProbeRequests = IpcRequestAssert.WithMethod(requests, UnityIpcMethod.Ping);
        Assert.NotEqual(Guid.Empty, IpcRequestAssert.SingleRequestId(startupProbeRequests));
        Assert.True(
            startupProbeRequests[1].RequestDeadlineRemainingMilliseconds
            < startupProbeRequests[0].RequestDeadlineRemainingMilliseconds);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);

        IpcResponse ThrowStartupResponseInterruption ()
        {
            firstPingCompleted.TrySetResult();
            throw new IpcResponseReadInterruptedException(
                new IOException("Pipe is broken."));
        }
    }

    private static IpcLifecycleExecutionStartRequest ReadStartRequest (
        IpcRequestEnvelope request)
    {
        Assert.True(
            IpcPayloadCodec.TryDeserialize(
                request.Payload,
                out IpcLifecycleExecutionStartRequest startRequest,
                out var readError),
            readError.Message);
        return startRequest;
    }
}
