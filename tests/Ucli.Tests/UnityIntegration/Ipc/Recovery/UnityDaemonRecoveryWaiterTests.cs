using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonRecoveryWaiterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenMatchingGuiSessionIsRecovering_DelaysAndReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession();
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading),
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(CreateContext(), deadline, CancellationToken.None).AsTask();
        Assert.False(delayTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        Assert.True(await delayTask);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenLifecycleSidecarIsMissing_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var waiter = CreateWaiter(
            CreateSession(),
            observation: null,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(CreateContext(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenEditorInstanceMatchesAndStartTimeDiffers_DelaysAndReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession();
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading) with
        {
            ProcessStartedAtUtc = session.ProcessStartedAtUtc!.Value.AddMilliseconds(1),
        };
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(CreateContext(), deadline, CancellationToken.None).AsTask();
        Assert.False(delayTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        Assert.True(await delayTask);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenEditorInstanceDiffers_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession();
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading) with
        {
            EditorInstanceId = "other-editor-instance",
        };
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(CreateContext(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenEditorInstanceIdsAreMissing_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession() with
        {
            EditorInstanceId = null,
        };
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading);
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(CreateContext(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenProcessIdentityDiffers_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession();
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleStateCodec.Recovering),
            DaemonProcessIdentityAssessmentStatus.DifferentProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(CreateContext(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenSessionIsBatchmode_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession("batchmode");
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading),
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(CreateContext(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    private static UnityDaemonRecoveryWaiter CreateWaiter (
        DaemonSession session,
        DaemonLifecycleObservation? observation,
        DaemonProcessIdentityAssessmentStatus processStatus,
        TimeProvider timeProvider)
    {
        return new UnityDaemonRecoveryWaiter(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(session)),
            new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(observation)),
            new StubDaemonProcessIdentityAssessor(processStatus),
            timeProvider);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (string editorMode = "gui")
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "project-fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: editorMode,
            OwnerKind: "user",
            CanShutdownProcess: false,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10),
            OwnerProcessId: null)
        {
            EditorInstanceId = "editor-instance-1",
        };
    }

    private static DaemonLifecycleObservation CreateObservation (
        DaemonSession session,
        string lifecycleState)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: session.EditorMode,
            LifecycleState: lifecycleState,
            BlockingReason: IpcEditorBlockingReasonCodec.DomainReload,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            EditorInstanceId = session.EditorInstanceId,
        };
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        private readonly DaemonSessionReadResult readResult;

        public StubDaemonSessionStore (DaemonSessionReadResult readResult)
        {
            this.readResult = readResult;
        }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(readResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonLifecycleStore : IDaemonLifecycleStore
    {
        private readonly DaemonLifecycleObservationReadResult readResult;

        public StubDaemonLifecycleStore (DaemonLifecycleObservationReadResult readResult)
        {
            this.readResult = readResult;
        }

        public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(readResult);
        }

        public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        private readonly DaemonProcessIdentityAssessmentStatus status;

        public StubDaemonProcessIdentityAssessor (DaemonProcessIdentityAssessmentStatus status)
        {
            this.status = status;
        }

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset? expectedProcessStartedAtUtc)
        {
            return new DaemonProcessIdentityAssessment(status, expectedProcessStartedAtUtc, null);
        }
    }
}
