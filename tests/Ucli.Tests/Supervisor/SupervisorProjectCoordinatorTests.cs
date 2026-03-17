using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Supervisor;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectCoordinatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenDaemonIsAlreadyRunning_SkipsStabilityVerification ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(CreateSession(process.Id)),
        };
        var pingClient = new StubDaemonPingClient();
        var stopOperation = new StubDaemonStopOperation();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            diagnosisStore,
            new StubDaemonSessionStore());

        var result = await coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(0, pingClient.PingCallCount);
        Assert.Equal(0, stopOperation.StopCallCount);

        StopProcess(process);
        await process.WaitForExitAsync();
        await coordinator.AwaitManagedProcesses();
        Assert.False(coordinator.HasManagedProjects);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenDaemonIsAlreadyRunningWithoutProcessId_TracksPidlessSessionUntilPingMonitorDetectsExit ()
    {
        var unityProject = CreateUnityProject();
        var releasePing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(CreateSession(processId: null)),
        };
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = async (_, _, cancellationToken) =>
            {
                await releasePing.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                throw new SocketException();
            },
        };
        var coordinator = CreateCoordinator(
            startOperation,
            new StubDaemonStopOperation(),
            pingClient,
            new StubDaemonDiagnosisStore(),
            new StubDaemonSessionStore());

        var result = await coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.True(coordinator.HasManagedProjects);

        releasePing.TrySetResult();
        await coordinator.AwaitManagedProcesses();
        Assert.False(coordinator.HasManagedProjects);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenStabilityVerificationIsCanceled_StopsStartedDaemonAndKeepsItManaged ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(CreateSession(process.Id)),
        };
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = async (_, _, cancellationToken) =>
            {
                pingStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            },
        };
        var stopOperation = new StubDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            diagnosisStore,
            new StubDaemonSessionStore());
        using var cancellationTokenSource = new CancellationTokenSource();

        var ensureRunningTask = coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                cancellationTokenSource.Token)
            .AsTask();
        await pingStarted.Task;
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await ensureRunningTask);

        await stopStarted.Task;
        Assert.Equal(1, stopOperation.StopCallCount);
        Assert.Equal(DaemonTimeouts.StopCompensationTimeout, stopOperation.LastTimeout);
        Assert.True(coordinator.HasManagedProjects);
        Assert.True(coordinator.HasActiveProjectWork);

        stopRelease.TrySetResult();
        StopProcess(process);
        await process.WaitForExitAsync();
        await coordinator.AwaitManagedProcesses();
        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenStabilityTimesOut_ReturnsBeforeBackgroundCompensationCompletes ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var timeProvider = new ManualTimeProvider();
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(CreateSession(process.Id)),
        };
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = (_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.CompletedTask;
            },
        };
        var stopOperation = new StubDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            diagnosisStore,
            new StubDaemonSessionStore(),
            timeProvider: timeProvider);

        var result = await coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(70),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.True(coordinator.HasActiveProjectWork);
        await stopStarted.Task;

        stopRelease.TrySetResult();
        StopProcess(process);
        await process.WaitForExitAsync();
        await coordinator.AwaitManagedProcesses();
        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenStabilityFails_ReturnsBeforeBackgroundCompensationCompletes ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(CreateSession(process.Id)),
        };
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = static (_, _, _) => ValueTask.FromException(new InvalidOperationException("ping failed")),
        };
        var stopOperation = new StubDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            new StubDaemonDiagnosisStore(),
            new StubDaemonSessionStore());

        var result = await coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.True(coordinator.HasActiveProjectWork);
        await stopStarted.Task;

        stopRelease.TrySetResult();
        StopProcess(process);
        await process.WaitForExitAsync();
        await coordinator.AwaitManagedProcesses();
        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StopProject_WhenBackgroundCompensationIsStillRunning_RespectsCallerTimeout ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var timeProvider = new ManualTimeProvider();
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(CreateSession(process.Id)),
        };
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = (_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.CompletedTask;
            },
        };
        var stopOperation = new StubDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            new StubDaemonDiagnosisStore(),
            new StubDaemonSessionStore(),
            timeProvider: timeProvider);

        var ensureRunningResult = await coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(70),
                CancellationToken.None);
        Assert.False(ensureRunningResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, ensureRunningResult.Error!.Kind);
        await stopStarted.Task;

        var stopTask = coordinator.StopProject(
                unityProject,
                TimeSpan.FromMilliseconds(50),
                CancellationToken.None)
            .AsTask();
        var stopResult = await stopTask;

        Assert.False(stopResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, stopResult.Error!.Kind);
        Assert.Equal(
            "Timed out while waiting for prior supervisor lifecycle cleanup to finish.",
            stopResult.Error.Message);
        Assert.Equal(1, stopOperation.StopCallCount);
        Assert.True(coordinator.HasActiveProjectWork);

        stopRelease.TrySetResult();
        StopProcess(process);
        await process.WaitForExitAsync();
        await coordinator.AwaitManagedProcesses();
        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StopProject_WhenStopFails_DoesNotSuppressUnexpectedExitDiagnosis ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var session = CreateSession(process.Id);
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var sessionStore = new StubDaemonSessionStore
        {
            Session = session,
        };
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(session),
        };
        var pingClient = new StubDaemonPingClient();
        var stopOperation = new StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.Failure(ExecutionError.Timeout("stop failed")),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            diagnosisStore,
            sessionStore);

        var ensureRunningResult = await coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None);
        Assert.True(ensureRunningResult.IsSuccess);

        var stopResult = await coordinator.StopProject(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None);

        Assert.False(stopResult.IsSuccess);

        StopProcess(process);
        await process.WaitForExitAsync();
        await coordinator.AwaitManagedProcesses();

        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.UnexpectedExit, diagnosisStore.LastDiagnosis!.Reason);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ManagedProjectCount_RemainsNonZeroUntilExitCleanupCompletes ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = CreateSession(process.Id);
        var sessionStore = new StubDaemonSessionStore
        {
            Session = session,
        };
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(session),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            new StubDaemonStopOperation(),
            new StubDaemonPingClient(),
            new StubDaemonDiagnosisStore(),
            sessionStore,
            new StubDaemonArtifactCleaner
            {
                CleanupHandler = async (_, _) =>
                {
                    cleanupStarted.TrySetResult();
                    await cleanupRelease.Task.ConfigureAwait(false);
                    return DaemonSessionStoreOperationResult.Success();
                },
            });

        var ensureRunningResult = await coordinator.EnsureRunning(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None);
        Assert.True(ensureRunningResult.IsSuccess);

        StopProcess(process);
        await cleanupStarted.Task;
        Assert.True(coordinator.HasManagedProjects);

        cleanupRelease.TrySetResult();
        await process.WaitForExitAsync();
        await coordinator.AwaitManagedProcesses();
        Assert.False(coordinator.HasManagedProjects);
    }

    private static SupervisorProjectCoordinator CreateCoordinator (
        IDaemonStartOperation startOperation,
        IDaemonStopOperation stopOperation,
        IDaemonPingClient pingClient,
        IDaemonDiagnosisStore diagnosisStore,
        IDaemonSessionStore sessionStore,
        IDaemonArtifactCleaner? artifactCleaner = null,
        TimeProvider? timeProvider = null)
    {
        var runtimeLogger = new SupervisorRuntimeLogger();
        var stabilityVerifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore),
            timeProvider);
        var exitHandler = new SupervisorExitHandler(
            sessionStore,
            artifactCleaner ?? new StubDaemonArtifactCleaner(),
            new SupervisorDiagnosisWriter(diagnosisStore),
            runtimeLogger);
        return new SupervisorProjectCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            new DaemonReachabilityClassifier(),
            stabilityVerifier,
            exitHandler,
            runtimeLogger,
            timeProvider);
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-supervisor-tests", Guid.NewGuid().ToString("N"));
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (int? processId)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: processId,
            OwnerProcessId: 9876);
    }

    private static Process StartLongRunningProcess ()
    {
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo("cmd", "/c ping -n 30 127.0.0.1 > NUL");
        }
        else
        {
            startInfo = new ProcessStartInfo("/bin/sh", "-c \"sleep 30\"");
        }

        startInfo.CreateNoWindow = true;
        startInfo.UseShellExecute = false;
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the long-running helper process.");
    }

    private static void StopProcess (Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (Win32Exception)
        {
        }
    }

    private sealed class StubDaemonStartOperation : IDaemonStartOperation
    {
        public DaemonStartResult StartResult { get; set; } = DaemonStartResult.Started(CreateSession(1234));

        public ValueTask<DaemonStartResult> Start (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(StartResult);
        }
    }

    private sealed class StubDaemonStopOperation : IDaemonStopOperation
    {
        public DaemonStopResult StopResult { get; set; } = DaemonStopResult.Stopped();

        public Func<ResolvedUnityProjectContext, TimeSpan, CancellationToken, ValueTask<DaemonStopResult>>? StopHandler { get; set; }

        public int StopCallCount { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<DaemonStopResult> Stop (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            LastTimeout = timeout;
            if (StopHandler != null)
            {
                return StopHandler(unityProject, timeout, cancellationToken);
            }

            return ValueTask.FromResult(StopResult);
        }
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        public Func<ResolvedUnityProjectContext, TimeSpan, CancellationToken, ValueTask>? PingHandler { get; set; }

        public int PingCallCount { get; private set; }

        public async ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            PingCallCount++;
            if (PingHandler != null)
            {
                await PingHandler(unityProject, timeout, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Write (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSession? Session { get; set; }

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionReadResult.Success(Session));
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            Session = session;
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            Session = null;
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public Func<ResolvedUnityProjectContext, CancellationToken, ValueTask<DaemonSessionStoreOperationResult>>? CleanupHandler { get; set; }

        public ValueTask<DaemonSessionStoreOperationResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            if (CleanupHandler != null)
            {
                return CleanupHandler(unityProject, cancellationToken);
            }

            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }
}