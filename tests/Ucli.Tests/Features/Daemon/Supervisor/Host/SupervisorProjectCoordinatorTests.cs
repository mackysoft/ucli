using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectCoordinatorTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(5);

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

        var result = await coordinator.EnsureRunningAsync(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                editorMode: null,
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(0, pingClient.PingCallCount);
        Assert.Equal(0, stopOperation.StopCallCount);

        StopProcess(process);
        await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
        await coordinator.AwaitManagedProcessesAsync();
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

        var result = await coordinator.EnsureRunningAsync(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                editorMode: null,
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.True(coordinator.HasManagedProjects);

        releasePing.TrySetResult();
        await coordinator.AwaitManagedProcessesAsync();
        Assert.False(coordinator.HasManagedProjects);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenUserOwnedGuiSessionIsAlreadyRunning_DoesNotRegisterManagedProcess ()
    {
        var unityProject = CreateUnityProject();
        var userOwnedSession = CreateSession(
            processId: 4242,
            editorMode: DaemonEditorModeValues.Gui,
            ownerKind: DaemonSessionOwnerKindValues.User,
            canShutdownProcess: false);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(userOwnedSession),
        };
        var pingClient = new StubDaemonPingClient();
        var coordinator = CreateCoordinator(
            startOperation,
            new StubDaemonStopOperation(),
            pingClient,
            new StubDaemonDiagnosisStore(),
            new StubDaemonSessionStore());

        var result = await coordinator.EnsureRunningAsync(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                editorMode: DaemonEditorMode.Gui,
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.False(coordinator.HasManagedProjects);
        Assert.False(coordinator.HasActiveProjectWork);
        Assert.Equal(0, pingClient.PingCallCount);
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

        var ensureRunningTask = coordinator.EnsureRunningAsync(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                editorMode: null,
                cancellationTokenSource.Token)
            .AsTask();
        try
        {
            await TestAwaiter.WaitAsync(pingStarted.Task, "Daemon stability ping start", SignalWaitTimeout);
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    ensureRunningTask,
                    "Canceled supervisor ensure-running result",
                    SignalWaitTimeout);
            });

            await TestAwaiter.WaitAsync(stopStarted.Task, "Daemon compensation stop start", SignalWaitTimeout);
            Assert.Equal(1, stopOperation.StopCallCount);
            Assert.Equal(DaemonTimeouts.StopCompensationTimeout, stopOperation.LastTimeout);
            Assert.True(coordinator.HasManagedProjects);
            Assert.True(coordinator.HasActiveProjectWork);
        }
        finally
        {
            stopRelease.TrySetResult();
            StopProcess(process);
            await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
            await coordinator.AwaitManagedProcessesAsync();
        }

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

        try
        {
            var result = await coordinator.EnsureRunningAsync(
                    unityProject,
                    TimeSpan.FromMilliseconds(70),
                    editorMode: null,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.True(coordinator.HasActiveProjectWork);
            await TestAwaiter.WaitAsync(stopStarted.Task, "Daemon timeout compensation stop start", SignalWaitTimeout);
        }
        finally
        {
            stopRelease.TrySetResult();
            StopProcess(process);
            await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
            await coordinator.AwaitManagedProcessesAsync();
        }

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

        try
        {
            var result = await coordinator.EnsureRunningAsync(
                    unityProject,
                    TimeSpan.FromMilliseconds(500),
                    editorMode: null,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
            Assert.True(coordinator.HasActiveProjectWork);
            await TestAwaiter.WaitAsync(stopStarted.Task, "Daemon failure compensation stop start", SignalWaitTimeout);
        }
        finally
        {
            stopRelease.TrySetResult();
            StopProcess(process);
            await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
            await coordinator.AwaitManagedProcessesAsync();
        }

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

        try
        {
            var ensureRunningResult = await coordinator.EnsureRunningAsync(
                    unityProject,
                    TimeSpan.FromMilliseconds(70),
                    editorMode: null,
                CancellationToken.None);
            Assert.False(ensureRunningResult.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, ensureRunningResult.Error!.Kind);
            await TestAwaiter.WaitAsync(stopStarted.Task, "Daemon stop failure compensation start", SignalWaitTimeout);

            var stopTask = coordinator.StopProjectAsync(
                    unityProject,
                    TimeSpan.FromMilliseconds(50),
                    CancellationToken.None)
                .AsTask();
            var stopResult = await TestAwaiter.WaitAsync(stopTask, "Supervisor stop project result", SignalWaitTimeout);

            Assert.False(stopResult.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, stopResult.Error!.Kind);
            Assert.Equal(
                "Timed out while waiting for prior supervisor lifecycle cleanup to finish.",
                stopResult.Error.Message);
            Assert.Equal(1, stopOperation.StopCallCount);
            Assert.True(coordinator.HasActiveProjectWork);
        }
        finally
        {
            stopRelease.TrySetResult();
            StopProcess(process);
            await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
            await coordinator.AwaitManagedProcessesAsync();
        }

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

        var ensureRunningResult = await coordinator.EnsureRunningAsync(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                editorMode: null,
                CancellationToken.None);
        Assert.True(ensureRunningResult.IsSuccess);

        var stopResult = await coordinator.StopProjectAsync(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None);

        Assert.False(stopResult.IsSuccess);

        StopProcess(process);
        await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
        await coordinator.AwaitManagedProcessesAsync();

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

        try
        {
            var ensureRunningResult = await coordinator.EnsureRunningAsync(
                    unityProject,
                    TimeSpan.FromMilliseconds(500),
                    editorMode: null,
                CancellationToken.None);
            Assert.True(ensureRunningResult.IsSuccess);

            StopProcess(process);
            await TestAwaiter.WaitAsync(cleanupStarted.Task, "Managed project cleanup start", SignalWaitTimeout);
            Assert.True(coordinator.HasManagedProjects);
        }
        finally
        {
            cleanupRelease.TrySetResult();
            await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
            await coordinator.AwaitManagedProcessesAsync();
        }

        Assert.False(coordinator.HasManagedProjects);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AwaitManagedProcesses_WhenExitCleanupFaults_DetachesFaultedMonitorTask ()
    {
        using var process = StartLongRunningProcess();
        var unityProject = CreateUnityProject();
        var session = CreateSession(process.Id);
        var sessionStore = new StubDaemonSessionStore
        {
            Session = session,
            ReadException = new InvalidOperationException("session read failed"),
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
            sessionStore);

        var ensureRunningResult = await coordinator.EnsureRunningAsync(
                unityProject,
                TimeSpan.FromMilliseconds(500),
                editorMode: null,
                CancellationToken.None);
        Assert.True(ensureRunningResult.IsSuccess);
        Assert.True(coordinator.HasManagedProjects);

        StopProcess(process);
        await TestProcessAwaiter.WaitForExitAsync(process, "Managed daemon helper process", ProcessExitTimeout);
        await TestAwaiter.WaitAsync(
            coordinator.AwaitManagedProcessesAsync(),
            "Supervisor await managed processes after exit cleanup fault",
            SignalWaitTimeout);

        Assert.False(coordinator.HasManagedProjects);
        Assert.False(coordinator.HasActiveProjectWork);
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

    private static DaemonSession CreateSession (
        int? processId,
        string editorMode = DaemonEditorModeValues.Batchmode,
        string ownerKind = DaemonSessionOwnerKindValues.Cli,
        bool canShutdownProcess = true)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            EditorMode: editorMode,
            OwnerKind: ownerKind,
            CanShutdownProcess: canShutdownProcess,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: processId,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
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

        public ValueTask<DaemonStartResult> StartAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            DaemonEditorMode? editorMode,
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

        public ValueTask<DaemonStopResult> StopAsync (
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

        public async ValueTask PingAsync (
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

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
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

        public Exception? ReadException { get; set; }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            if (ReadException != null)
            {
                return ValueTask.FromException<DaemonSessionReadResult>(ReadException);
            }

            return ValueTask.FromResult(DaemonSessionReadResult.Success(Session));
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            Session = session;
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
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

        public ValueTask<DaemonSessionStoreOperationResult> CleanupAsync (
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
