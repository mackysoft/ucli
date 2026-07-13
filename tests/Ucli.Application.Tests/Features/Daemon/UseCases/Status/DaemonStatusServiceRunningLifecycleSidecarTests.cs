using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceRunningLifecycleSidecarTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningPingInfoTimesOutAndFreshLifecycleSidecarExists_ReturnsRunningObservation ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2485);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create() with
        {
            EditorMode = DaemonEditorMode.Gui,
            EditorInstanceId = "editor-instance-1",
        };
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(session)),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                ObservedStartTimeUtc: session.ProcessStartedAtUtc,
                Error: null),
        };
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new TimeoutException("ping timeout"));
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver(),
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal(IpcEditorLifecycleState.PlayMode, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.PlayMode, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal(IpcPlayModeState.Playing, output.PlayMode!.State);
        DaemonStatusServiceInvocationAssert.FreshLifecycleSidecarUsed(lifecycleStore, processIdentityAssessor, context, session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningPingInfoTimesOutAndLifecycleSidecarLacksEditorInstanceId_ReturnsStaleStatus ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2485);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create() with
        {
            EditorMode = DaemonEditorMode.Gui,
            EditorInstanceId = "editor-instance-1",
        };
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(session, includeEditorInstanceId: false)),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                ObservedStartTimeUtc: session.ProcessStartedAtUtc,
                Error: null),
        };
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new TimeoutException("ping timeout"));
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver(),
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        DaemonStatusServiceInvocationAssert.LifecycleSidecarReadWithoutProcessIdentityAssessment(lifecycleStore, processIdentityAssessor, context);
    }
}
