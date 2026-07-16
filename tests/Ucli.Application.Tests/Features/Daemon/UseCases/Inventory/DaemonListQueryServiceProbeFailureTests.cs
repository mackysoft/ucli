using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Shared.Git;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonListQueryServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonListQueryServiceProbeFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSessionTokenRotatesDuringProbe_RetriesWithRefreshedSessionMetadata ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var firstSession = DaemonSessionTestFactory.Create(
            sessionToken: "first-token",
            projectFingerprint: currentProject.ProjectFingerprint,
            endpointAddress: "first-endpoint",
            processId: 2001);
        var refreshedSession = DaemonSessionTestFactory.Create(
            sessionToken: "refreshed-token",
            projectFingerprint: currentProject.ProjectFingerprint,
            issuedAtUtc: firstSession.IssuedAtUtc.AddSeconds(1),
            endpointAddress: "refreshed-endpoint",
            processId: 2002,
            sessionGenerationId: Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(firstSession)
                : DaemonSessionReadResultTestFactory.Found(refreshedSession),
        };
        var pingResponse = CreatePingResponse(currentProject);
        var pingClient = new RecordingDaemonPingInfoClient(
            new SessionTokenInvalidTestException(),
            pingResponse);
        var service = CreateService(
            new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            RecordingUnityProjectResolver.FromContexts(currentProject),
            sessionStore,
            new RecordingDaemonDiagnosisStore(),
            pingClient,
            new SessionTokenRotationReachabilityClassifier());

        var result = await service.GetListAsync(
            currentProject,
            TimeSpan.FromMilliseconds(1200),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Output!.Items);
        Assert.Equal(DaemonListItemState.Running, item.State);
        Assert.Equal(refreshedSession.ProcessId, item.ProcessId);
        Assert.Equal(refreshedSession.Endpoint.Address, item.EndpointAddress);
        Assert.Equal(2, sessionStore.ReadInvocations.Count);
        Assert.Collection(
            pingClient.Invocations,
            invocation => Assert.Equal(firstSession, invocation.Session),
            invocation => Assert.Equal(refreshedSession, invocation.Session));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Small")]
    public async Task List_WhenReplacementSessionProbeFails_AttributesItemToReplacementSession (
        bool probeTimesOut)
    {
        var currentProject = CreateUnityProject(
            "/repo/wt-current",
            "UnityProject",
            $"fp-replacement-failure-{probeTimesOut}");
        var observedSession = DaemonSessionTestFactory.Create(
            sessionToken: "observed-token",
            projectFingerprint: currentProject.ProjectFingerprint,
            endpointAddress: "observed-endpoint",
            processId: 2011);
        var replacementSession = DaemonSessionTestFactory.Create(
            sessionToken: "replacement-token",
            projectFingerprint: currentProject.ProjectFingerprint,
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1),
            endpointAddress: "replacement-endpoint",
            processId: 2012,
            sessionGenerationId: Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(observedSession)
                : DaemonSessionReadResultTestFactory.Found(replacementSession),
        };
        Exception replacementFailure = probeTimesOut
            ? new TimeoutException("Replacement probe timed out.")
            : new SocketException((int)SocketError.ConnectionRefused);
        var pingClient = new RecordingDaemonPingInfoClient(
            new SessionTokenInvalidTestException(),
            replacementFailure);
        var service = CreateService(
            new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            RecordingUnityProjectResolver.FromContexts(currentProject),
            sessionStore,
            new RecordingDaemonDiagnosisStore(),
            pingClient,
            new SessionTokenRotationReachabilityClassifier());

        var result = await service.GetListAsync(
            currentProject,
            TimeSpan.FromMilliseconds(1200),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Output!.Items);
        Assert.Equal(
            probeTimesOut ? DaemonListItemState.Error : DaemonListItemState.Stale,
            item.State);
        Assert.Equal(
            probeTimesOut ? DaemonListItemReason.ProbeTimeout : DaemonListItemReason.StaleSession,
            item.Reason);
        Assert.Equal(replacementSession.ProcessId, item.ProcessId);
        Assert.Equal(replacementSession.Endpoint.Address, item.EndpointAddress);
        Assert.Collection(
            pingClient.Invocations,
            invocation => Assert.Equal(observedSession, invocation.Session),
            invocation => Assert.Equal(replacementSession, invocation.Session));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeTimesOut_ReturnsProbeTimeoutItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fp-current"),
            endpointAddress: "endpoint-timeout",
            processId: 2100);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new TimeoutException("probe timed out")),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.ProbeTimeout, item.Reason);
        Assert.Equal(2100, item.ProcessId);
        Assert.Equal("endpoint-timeout", item.EndpointAddress);
        Assert.Null(item.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeTimesOutWithFreshRecoveringSidecar_DoesNotReportRunningOrLifecycleTelemetry ()
    {
        var now = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateGuiSession(ProjectFingerprintTestFactory.Create("fp-current"), processId: 2101);
        var lifecycleStore = CreateRecoveringLifecycleStore(session, now);
        var processIdentityAssessor = RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(session.ProcessStartedAtUtc!.Value);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new TimeoutException("probe timed out")),
            new StubDaemonReachabilityClassifier(static _ => false),
            new ManualTimeProvider(now),
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Output!.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.ProbeTimeout, item.Reason);
        AssertUnreachableLifecycle(item);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeIsUnreachableWithFreshRecoveringSidecar_ReturnsStaleWithoutExternalTerminationDiagnosis ()
    {
        var now = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateGuiSession(ProjectFingerprintTestFactory.Create("fp-current"), processId: 2201);
        var lifecycleStore = CreateRecoveringLifecycleStore(session, now);
        var processIdentityAssessor = RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(session.ProcessStartedAtUtc!.Value);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException),
            new ManualTimeProvider(now),
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Output!.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        AssertUnreachableLifecycle(item);
        Assert.Null(item.Diagnosis);
        Assert.Single(lifecycleStore.ReadInvocations);
        Assert.Single(processIdentityAssessor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenRecoveringSidecarIsStale_DoesNotUseItAsLiveProcessEvidence ()
    {
        var now = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateGuiSession(ProjectFingerprintTestFactory.Create("fp-current"), processId: 2202);
        var lifecycleStore = CreateRecoveringLifecycleStore(
            session,
            now - DaemonLifecycleObservationTimings.FreshnessWindow - TimeSpan.FromMilliseconds(1));
        var processIdentityAssessor = RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(session.ProcessStartedAtUtc!.Value);
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            diagnosisStore,
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException),
            new ManualTimeProvider(now),
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Output!.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        AssertUnreachableLifecycle(item);
        Assert.Equal(DaemonDiagnosisReason.ExternalTerminationSuspected, item.Diagnosis!.Reason);
        Assert.Empty(processIdentityAssessor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeClassifiesNotRunningAndPersistedDiagnosisMatches_ReturnsStaleItemWithDiagnosis ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fp-current"),
            endpointAddress: "endpoint-stale",
            processId: 2200);
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReason.ShutdownRequested);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            OnRead = (_, _) => DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            diagnosisStore,
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        Assert.Equal(2200, item.ProcessId);
        Assert.NotNull(item.Diagnosis);
        Assert.Equal(diagnosis.Reason, item.Diagnosis!.Reason);
        Assert.Equal(diagnosis.Message, item.Diagnosis.Message);
        Assert.Equal(diagnosis.ReportedBy, item.Diagnosis.ReportedBy);
        Assert.Equal(diagnosis.IsInferred, item.Diagnosis.IsInferred);
        Assert.Equal(diagnosis.UpdatedAtUtc, item.Diagnosis.UpdatedAtUtc);
        Assert.Equal(diagnosis.ProcessId, item.Diagnosis.ProcessId);
        Assert.Equal(diagnosis.ProcessStartedAtUtc, item.Diagnosis.ProcessStartedAtUtc);
        Assert.Equal(diagnosis.UnityLogPath, item.Diagnosis.UnityLogPath);
        Assert.Equal(diagnosis.StartupPhase, item.Diagnosis.StartupPhase);
        Assert.Equal(diagnosis.ActionRequired, item.Diagnosis.ActionRequired);
        Assert.NotNull(item.Diagnosis.PrimaryDiagnostic);
        Assert.Equal(diagnosis.PrimaryDiagnostic!.Kind, item.Diagnosis.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeClassifiesNotRunningWithoutPersistedDiagnosis_ReturnsExternalTerminationDiagnosis ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fp-current"),
            endpointAddress: "endpoint-stale",
            processId: int.MaxValue);
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            diagnosisStore,
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        Assert.NotNull(item.Diagnosis);
        Assert.Equal(DaemonDiagnosisReason.ExternalTerminationSuspected, item.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedBy.Cli, item.Diagnosis.ReportedBy);
        Assert.True(item.Diagnosis.IsInferred);
        Assert.Equal(session.ProcessId, item.Diagnosis.ProcessId);
        DaemonDiagnosisStoreAssert.WrittenOnceWithReason(
            diagnosisStore,
            DaemonDiagnosisReason.ExternalTerminationSuspected);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeFailsUnexpectedly_ReturnsProbeFailedItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fp-current"),
            endpointAddress: "endpoint-failed",
            processId: 2300);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new InvalidOperationException("boom")),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.ProbeFailed, item.Reason);
        Assert.Equal(2300, item.ProcessId);
        Assert.Null(item.Diagnosis);
    }

    private static DaemonSession CreateGuiSession (
        ProjectFingerprint projectFingerprint,
        int processId)
    {
        return DaemonSessionTestFactory.Create(
            projectFingerprint: projectFingerprint,
            processId: processId,
            editorMode: DaemonEditorMode.Gui,
            editorInstanceId: Guid.NewGuid());
    }

    private static RecordingDaemonLifecycleStore CreateRecoveringLifecycleStore (
        DaemonSession session,
        DateTimeOffset observedAtUtc)
    {
        return new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(new DaemonLifecycleObservation(
                processId: session.ProcessId!.Value,
                processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
                state: new UnityEditorStateSnapshot(
                    editorMode: session.EditorMode,
                    lifecycleState: IpcEditorLifecycleState.Recovering,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                    playMode: new IpcPlayModeSnapshot(
                        IpcPlayModeState.Stopped,
                        IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: observedAtUtc,
                actionRequired: null,
                primaryDiagnostic: null,
                serverVersion: null,
                editorInstanceId: session.EditorInstanceId
                    ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)),
                recoveryLease: null)),
        };
    }

    private static void AssertUnreachableLifecycle (DaemonListItemOutput item)
    {
        Assert.Null(item.LifecycleState);
        Assert.Null(item.BlockingReason);
        Assert.Null(item.CompileState);
        Assert.Null(item.Generations);
        Assert.False(item.CanAcceptExecutionRequests);
        Assert.Null(item.ObservedAtUtc);
        Assert.Null(item.ActionRequired);
        Assert.Null(item.PrimaryDiagnostic);
    }

    private sealed class SessionTokenInvalidTestException : Exception
    {
    }

    private sealed class SessionTokenRotationReachabilityClassifier : IDaemonReachabilityClassifier
    {
        public bool IsNotRunning (Exception exception)
        {
            return exception is SessionTokenInvalidTestException or SocketException;
        }

        public bool IsSessionTokenInvalid (Exception exception)
        {
            return exception is SessionTokenInvalidTestException;
        }

        public bool IsRetryableBeforeRequestWrite (Exception exception)
        {
            return false;
        }

        public bool IsRequestTimeout (Exception exception)
        {
            return exception is TimeoutException;
        }

        public bool IsRecoverableResponseInterruption (Exception exception)
        {
            return false;
        }
    }
}
