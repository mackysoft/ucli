using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientCleanupTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenRequestTransportTimesOut_SendsShutdownBeforeTermination ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "request-timeout");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
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
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead, UnityIpcMethod.Shutdown);
        AssertCleanupShutdownUsesLaunchSession(launcher, transportClient, unityProject);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenCleanupShutdownResponseReadIsInterrupted_RetriesWithSameRequestId ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "shutdown-response-read-interrupted");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var shutdownAttempt = 0;
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
                UnityIpcMethod.Shutdown when Interlocked.Increment(ref shutdownAttempt) == 1 =>
                    throw new IpcResponseReadInterruptedException(new IOException("shutdown response was lost")),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var shutdownRequests = IpcRequestAssert.WithMethod(transportClient, UnityIpcMethod.Shutdown);
        Assert.Equal(2, shutdownRequests.Count);
        var requestId = shutdownRequests[0].RequestId;
        Assert.NotEqual(Guid.Empty, requestId);
        Assert.All(shutdownRequests, request => Assert.Equal(requestId, request.RequestId));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartupTimeoutReachesEndpointDuringCleanup_SendsShutdownWithoutTerminatingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-timeout-shutdown");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var cleanupTimeout = TimeSpan.FromMilliseconds(20);
        var requestTimeout = TimeSpan.FromMilliseconds(25);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => throw new TimeoutException("startup ping timed out"),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            cleanupTimeout,
            TimeSpan.FromMilliseconds(1),
            timeProvider);
        var launchDeadlineReferenceUtc = timeProvider.GetUtcNow();

        var resultTask = client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            requestTimeout,
            CancellationToken.None).AsTask();
        await ManualTimeTaskDriver.WaitForTimerDueWithinOrCompletionAsync(
            timeProvider,
            resultTask,
            requestTimeout);
        timeProvider.Advance(requestTimeout);
        var result = await TestAwaiter.WaitAsync(
            resultTask,
            "Unity oneshot startup timeout cleanup result",
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        var startupFailure = result.FailureInfo.StartupFailure!;
        Assert.Equal("timeout", startupFailure.Startup!.StartupStatus);
        Assert.Equal("endpointNotRegistered", startupFailure.Startup.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown), startupFailure.Startup.ProcessAction);
        Assert.Equal("startupFailed", startupFailure.Diagnosis!.Reason);
        AssertCleanupShutdownsUseLaunchSession(launcher, transportClient, unityProject, launchDeadlineReferenceUtc);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenResponseArrivesBeforeRequestDeadline_WaitsForCleanupExitWithCleanupBudget ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "response-before-deadline-cleanup-budget");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(60));
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient((request, cancellationToken) =>
        {
            if (IpcRequestAssert.ParseMethod(request) == UnityIpcMethod.Ping)
            {
                return ValueTask.FromResult(CreatePingResponse(request.RequestId));
            }

            if (IpcRequestAssert.ParseMethod(request) == UnityIpcMethod.OpsRead)
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(10));
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(CreateSuccessResponse(request.RequestId));
            }

            throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}");
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(1),
            timeProvider: timeProvider);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenResponseArrivesButProcessDoesNotExit_ReturnsSuccessAndTerminatesProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "response-success-process-stays-alive");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead, UnityIpcMethod.Shutdown);
        UnityBatchmodeProcessHandleAssert.WaitedForExitAndTerminatedOnceWithMode(
            processHandle,
            ProcessTerminationMode.GracefulThenKill);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenCompletedResponseCleanupCannotConfirmExit_RetainsProcessHandleUntilExit ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "response-success-exit-unconfirmed");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var releaseOwnedProcess = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handleDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitCount = 0;
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken =>
            {
                return Interlocked.Increment(ref waitCount) <= 2
                    ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    : releaseOwnedProcess.Task.WaitAsync(cancellationToken);
            })
        {
            TerminateHandler = static (_, _) => Task.FromResult(ProcessTerminationResult.ForceKillFailed),
            OnDispose = handleDisposed.SetResult,
        };
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        try
        {
            var result = await client.SendAsync(
                unityProject,
                CreateDispatchRequest(),
                TimeSpan.FromSeconds(30),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            UnityBatchmodeProcessHandleAssert.TerminatedOnceWithMode(
                processHandle,
                ProcessTerminationMode.GracefulThenKill);
            Assert.Equal(0, processHandle.DisposeCount);
        }
        finally
        {
            releaseOwnedProcess.TrySetResult();
        }

        await TestAwaiter.WaitAsync(
            handleDisposed.Task,
            "Successful oneshot process handle disposal",
            TimeSpan.FromSeconds(5));
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenPostResponseExitWaitThrows_ReturnsSuccessAndTerminatesProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "post-response-exit-wait-throws");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var waitCount = 0;
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken =>
            {
                waitCount++;
                return waitCount == 1
                    ? Task.FromException(new InvalidOperationException("exit wait failed"))
                    : Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        UnityBatchmodeProcessHandleAssert.WaitedForExitAndTerminatedOnceWithMode(
            processHandle,
            ProcessTerminationMode.GracefulThenKill);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenCallerCancelsAfterNonPingResponse_ReturnsSuccessAndTerminatesProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "post-response-caller-cancels");
        using var cancellationTokenSource = new CancellationTokenSource();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var waitCount = 0;
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken =>
            {
                waitCount++;
                if (waitCount == 1)
                {
                    cancellationTokenSource.Cancel();
                    return Task.FromCanceled(cancellationTokenSource.Token);
                }

                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            cancellationTokenSource.Token);

        Assert.True(result.IsSuccess);
        UnityBatchmodeProcessHandleAssert.WaitedForExitAndTerminatedOnceWithMode(
            processHandle,
            ProcessTerminationMode.GracefulThenKill);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenReadyPingResponseArrivesButProcessDoesNotExit_ReturnsIpcTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "ready-ping-process-stays-alive");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
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
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateReadyPingDispatchRequest(failFast: false),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Contains("did not exit", result.Message, StringComparison.Ordinal);
        AssertTerminalPingAndCleanupShutdownRequests(transportClient);
        UnityBatchmodeProcessHandleAssert.WaitedForExitAndTerminatedOnce(processHandle);

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
    public async Task SendAsync_WhenCleanupShutdownIsAcceptedButProcessDoesNotExit_TerminatesProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "shutdown-accepted-process-stays-alive");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead, UnityIpcMethod.Shutdown);
        AssertCleanupShutdownUsesLaunchSession(launcher, transportClient, unityProject);
        UnityBatchmodeProcessHandleAssert.TerminatedOnceWithMode(processHandle, ProcessTerminationMode.GracefulThenKill);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenForceKillCannotConfirmExit_DoesNotRunPostExitLockCleanupAndAppendsDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "force-kill-exit-unconfirmed");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");
        var releaseOwnedProcess = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handleDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitCount = 0;
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken =>
            {
                return Interlocked.Increment(ref waitCount) == 1
                    ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    : releaseOwnedProcess.Task.WaitAsync(cancellationToken);
            })
        {
            OnDispose = handleDisposed.SetResult,
        };
        processHandle.TerminateHandler = static (_, _) => Task.FromResult(ProcessTerminationResult.ForceKillFailed);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new IpcResponseReadInterruptedException(
                    new EndOfStreamException("IPC stream ended before a complete frame was read.")),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var projectLockPreflightService = CreateProjectLockPreflightService(
            UnityProjectLockFileProbeResult.Locked(lockFilePath));
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            projectLockPreflightService,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        try
        {
            var result = await client.SendAsync(
                unityProject,
                CreateDispatchRequest(),
                TimeSpan.FromSeconds(30),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
            Assert.Equal(UnityRequestFailureKind.General, result.FailureInfo!.FailureKind);
            Assert.StartsWith(
                "Failed to execute Unity oneshot IPC request. IPC stream ended before a complete frame was read.",
                result.Message,
                StringComparison.Ordinal);
            Assert.Contains("could not be confirmed stopped", result.Message, StringComparison.Ordinal);
            Assert.Empty(projectLockPreflightService.CleanupInvocations);
            UnityBatchmodeProcessHandleAssert.TerminatedOnceWithMode(processHandle, ProcessTerminationMode.GracefulThenKill);
            Assert.Equal(0, processHandle.DisposeCount);
        }
        finally
        {
            releaseOwnedProcess.TrySetResult();
        }

        await TestAwaiter.WaitAsync(
            handleDisposed.Task,
            "Transferred oneshot process handle disposal",
            TimeSpan.FromSeconds(5));
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenResponseReadIsInterruptedAndPostExitCleanupAddsNoDiagnostic_PreservesTransportInterruption ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "response-read-interrupted-clean-exit");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new IpcResponseReadInterruptedException(new IOException("Pipe is broken.")),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal("Failed to execute Unity oneshot IPC request. Pipe is broken.", result.Message);
        UnityBatchmodeProcessHandleAssert.TerminatedOnce(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenProcessTerminationThrows_PreservesPrimaryFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "termination-throws");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        processHandle.TerminateHandler = static (_, _) => Task.FromException<ProcessTerminationResult>(
            new InvalidOperationException("termination failed"));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.StartsWith(
            "Unity oneshot IPC request timed out after 30000 milliseconds.",
            result.Message,
            StringComparison.Ordinal);
        Assert.Contains("process cleanup did not complete", result.Message, StringComparison.Ordinal);
        Assert.Contains("termination failed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenPostExitLockCleanupThrows_PreservesPrimaryFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "post-exit-lock-cleanup-throws");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        processHandle.TerminateHandler = static (_, _) => Task.FromResult(ProcessTerminationResult.ForceKilled);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var projectLockPreflightService = CreateProjectLockPreflightService();
        projectLockPreflightService.CleanupAsyncHandler = static (_, _) =>
            ValueTask.FromException<UnityProjectLockPreflightResult>(new IOException("lock cleanup failed"));
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            projectLockPreflightService,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.StartsWith(
            "Unity oneshot IPC request timed out after 30000 milliseconds.",
            result.Message,
            StringComparison.Ordinal);
        Assert.Contains("Post-exit Unity project lock cleanup failed", result.Message, StringComparison.Ordinal);
        Assert.Contains("lock cleanup failed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenProcessHandleDisposalThrows_PreservesPrimaryFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "process-handle-dispose-throws");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        processHandle.TerminateHandler = static (_, _) => Task.FromResult(ProcessTerminationResult.ForceKilled);
        processHandle.OnDispose = static () => throw new InvalidOperationException("dispose failed");
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.StartsWith(
            "Unity oneshot IPC request timed out after 30000 milliseconds.",
            result.Message,
            StringComparison.Ordinal);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenLifecycleLockDisposalThrows_PreservesPrimaryFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "lifecycle-lock-dispose-throws");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
                UnityIpcMethod.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var lifecycleLock = new ThrowingAsyncDisposable();
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider((_, _, _) => lifecycleLock),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.StartsWith(
            "Unity oneshot IPC request timed out after 30000 milliseconds.",
            result.Message,
            StringComparison.Ordinal);
        Assert.Equal(1, lifecycleLock.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenCleanupTerminatesProcessAndStaleUnityLockFileRemains_ReturnsOriginalFailureWithCleanupDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "request-timeout-stale-lock");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => throw new TimeoutException("request timed out"),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(UnityProjectLockFileProbeResult.Locked(lockFilePath)));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Contains("Stale Unity project lock file was removed", result.Message, StringComparison.Ordinal);
        Assert.Contains(lockFilePath, result.Message, StringComparison.Ordinal);
        UnityBatchmodeProcessHandleAssert.TerminatedOnce(processHandle);
    }

    private sealed class ThrowingAsyncDisposable : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            throw new InvalidOperationException("lifecycle lock disposal failed");
        }
    }
}
