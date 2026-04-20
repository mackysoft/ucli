using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;

public sealed class DaemonLaunchServiceTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAndReadinessSucceed_ReturnsStarted ()
    {
        var context = CreateContext("fingerprint-launch-success");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var startedSession = initialSession with { ProcessId = 999 };
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(startedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(999),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Ready(),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(startedSession, result.Session);
        Assert.Equal(1, launchSessionService.InitializeCallCount);
        Assert.Equal(1, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(1, launcher.CallCount);
        Assert.Equal(0, compensationService.CallCount);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionInitializationFails_ReturnsFailureWithoutLaunch ()
    {
        var context = CreateContext("fingerprint-launch-init-fail");
        var expectedError = ExecutionError.InternalError("session init failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Failure(expectedError),
        };
        var launcher = new StubUnityDaemonProcessLauncher();
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, launchSessionService.InitializeCallCount);
        Assert.Equal(0, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(0, launcher.CallCount);
        Assert.Equal(0, compensationService.CallCount);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityLaunchFails_RunsCompensationAndReturnsLaunchFailure ()
    {
        var context = CreateContext("fingerprint-launch-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Null(compensationService.LastProcessId);
        Assert.Equal(initialSession.IssuedAtUtc, compensationService.LastExpectedIssuedAtUtc);
        Assert.Equal(0, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.StartupFailed, diagnosisStore.LastDiagnosis!.Reason);
        Assert.Equal(launchError.Message, diagnosisStore.LastDiagnosis.Message);
        Assert.Equal(initialSession.IssuedAtUtc, diagnosisStore.LastDiagnosis.SessionIssuedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionUpdateFails_RunsCompensationAndReturnsWriteFailure ()
    {
        var context = CreateContext("fingerprint-session-update-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var writeError = ExecutionError.InternalError("write failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Failure(writeError),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(2222),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(writeError, result.Error);
        Assert.Equal(1, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(2222, compensationService.LastProcessId);
        Assert.Equal(initialSession.IssuedAtUtc, compensationService.LastExpectedIssuedAtUtc);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeFails_RunsCompensationAndReturnsProbeFailure ()
    {
        var context = CreateContext("fingerprint-probe-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var probeError = ExecutionError.Timeout("probe failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(probeError, result.Error);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(7777, compensationService.LastProcessId);
        Assert.Equal(updatedSession.IssuedAtUtc, compensationService.LastExpectedIssuedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCompensationFails_ReturnsInternalError ()
    {
        var context = CreateContext("fingerprint-launch-compensation-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed");
        var cleanupError = ExecutionError.InternalError("cleanup failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(cleanupError),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Daemon launch failed and cleanup failed.", error.Message, StringComparison.Ordinal);
        Assert.Contains("LaunchError=launch failed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosisError=", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchFailureOccursAfterDeadline_StillRunsCompensation ()
    {
        var context = CreateContext("fingerprint-launch-timeout-compensation");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var timeProvider = new ManualTimeProvider();
        var launchError = ExecutionError.InternalError("launch failed after timeout");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            LaunchDelay = TimeSpan.FromMilliseconds(50),
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
            TimeProvider = timeProvider,
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore,
            timeProvider);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(1), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisWriteFails_PreservesPrimaryTimeoutErrorKind ()
    {
        var context = CreateContext("fingerprint-diagnosis-write-timeout");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var probeError = ExecutionError.Timeout("probe failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("diagnosis write failed")),
        };
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("DiagnosisError=diagnosis write failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCleanupFails_PreservesPrimaryTimeoutErrorKind ()
    {
        var context = CreateContext("fingerprint-cleanup-timeout");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var probeError = ExecutionError.Timeout("probe failed");
        var cleanupError = ExecutionError.InternalError("cleanup failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(cleanupError),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCancellationRequestedAfterLaunchFailure_StillRunsCompensation ()
    {
        var context = CreateContext("fingerprint-launch-cancel-after-failure");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed after cancel");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        using var cancellationSource = new CancellationTokenSource();
        var launcher = new StubUnityDaemonProcessLauncher
        {
            OnLaunch = cancellationSource.Cancel,
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), cancellationSource.Token);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        Assert.True(cancellationSource.IsCancellationRequested);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCanceledDuringReadinessProbe_RunsCompensationThenThrows ()
    {
        var context = CreateContext("fingerprint-launch-cancel-during-readiness");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777),
        };
        using var cancellationSource = new CancellationTokenSource();
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            OnWaitUntilReady = cancellationSource.Cancel,
            NextException = new OperationCanceledException(cancellationSource.Token),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.Launch(context, TimeSpan.FromMilliseconds(500), cancellationSource.Token).AsTask(),
                "Canceled daemon launch result",
                AsyncWaitTimeout);
        });

        Assert.True(cancellationSource.IsCancellationRequested);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(7777, compensationService.LastProcessId);
        Assert.Equal(updatedSession.IssuedAtUtc, compensationService.LastExpectedIssuedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisWriteFails_ReturnsInternalError ()
    {
        var context = CreateContext("fingerprint-launch-diagnosis-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed");
        var diagnosisError = ExecutionError.InternalError("diagnosis failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(diagnosisError),
        };
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.Launch(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Daemon launch failed and diagnosis persistence failed.", error.Message, StringComparison.Ordinal);
        Assert.Contains("LaunchError=launch failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("DiagnosisError=diagnosis failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    private static DaemonLaunchService CreateService (
        IDaemonLaunchSessionService launchSessionService,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonLaunchCompensationService launchCompensationService,
        IDaemonDiagnosisStore? daemonDiagnosisStore = null,
        TimeProvider? timeProvider = null)
    {
        return new DaemonLaunchService(
            daemonLaunchSessionService: launchSessionService,
            unityDaemonProcessLauncher: unityDaemonProcessLauncher,
            startupReadinessProbe: startupReadinessProbe,
            daemonLaunchCompensationService: launchCompensationService,
            daemonDiagnosisStore: daemonDiagnosisStore ?? new StubDaemonDiagnosisStore(),
            timeProvider: timeProvider);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        int? processId,
        string projectFingerprint = "fingerprint")
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,

            OwnerProcessId: 9876);
    }

    private sealed class StubDaemonLaunchSessionService : IDaemonLaunchSessionService
    {
        public DaemonLaunchSessionWriteResult InitializeResult { get; set; } = DaemonLaunchSessionWriteResult.Success(CreateSession(processId: null));

        public DaemonLaunchSessionWriteResult? UpdateProcessIdResult { get; set; }

        public int InitializeCallCount { get; private set; }

        public int UpdateProcessIdCallCount { get; private set; }

        public ValueTask<DaemonLaunchSessionWriteResult> Initialize (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return ValueTask.FromResult(InitializeResult);
        }

        public ValueTask<DaemonLaunchSessionWriteResult> UpdateProcessId (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            int? processId,
            CancellationToken cancellationToken = default)
        {
            UpdateProcessIdCallCount++;
            if (UpdateProcessIdResult is not null)
            {
                return ValueTask.FromResult(UpdateProcessIdResult);
            }

            var updatedSession = processId is int pid
                ? session with { ProcessId = pid }
                : session;
            return ValueTask.FromResult(DaemonLaunchSessionWriteResult.Success(updatedSession));
        }
    }

    private sealed class StubUnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
    {
        public Action? OnLaunch { get; set; }

        public TimeSpan LaunchDelay { get; set; }

        public ManualTimeProvider? TimeProvider { get; set; }

        public UnityDaemonLaunchResult NextResult { get; set; } = UnityDaemonLaunchResult.Success(1000);

        public int CallCount { get; private set; }

        public async ValueTask<UnityDaemonLaunchResult> Launch (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            string daemonLogPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            OnLaunch?.Invoke();
            if (LaunchDelay > TimeSpan.Zero)
            {
                if (TimeProvider != null)
                {
                    TimeProvider.Advance(LaunchDelay);
                }
                else
                {
                    throw new InvalidOperationException("ManualTimeProvider is required when LaunchDelay is configured.");
                }
            }

            return NextResult;
        }
    }

    private sealed class StubDaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
    {
        public DaemonStartupReadinessProbeResult NextResult { get; set; } = DaemonStartupReadinessProbeResult.Ready();

        public Action? OnWaitUntilReady { get; set; }

        public Exception? NextException { get; set; }

        public ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReady (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            int? daemonProcessId = null,
            CancellationToken cancellationToken = default)
        {
            OnWaitUntilReady?.Invoke();
            if (NextException is not null)
            {
                return ValueTask.FromException<DaemonStartupReadinessProbeResult>(NextException);
            }

            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } = DaemonDiagnosisStoreOperationResult.Success();

        public int WriteCallCount { get; private set; }

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
            WriteCallCount++;
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonLaunchCompensationService : IDaemonLaunchCompensationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public int? LastProcessId { get; private set; }

        public DateTimeOffset? LastExpectedIssuedAtUtc { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunch (
            ResolvedUnityProjectContext unityProject,
            int? processId,
            DateTimeOffset? expectedIssuedAtUtc,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProcessId = processId;
            LastExpectedIssuedAtUtc = expectedIssuedAtUtc;
            LastTimeout = timeout;
            return ValueTask.FromResult(NextResult);
        }
    }
}