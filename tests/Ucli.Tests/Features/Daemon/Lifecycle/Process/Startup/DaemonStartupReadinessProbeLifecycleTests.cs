using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using static DaemonStartupReadinessProbeTestSupport;

public sealed class DaemonStartupReadinessProbeLifecycleTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingSucceeds_ReturnsReadyWithoutLogInspection ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload());
        var logReader = new UnexpectedUnityLogReader("Ready ping success should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-success")),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(UnityEditorLifecycleState.Ready, result.LifecycleObservation!.State.LifecycleState);
        Assert.True(UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsStarting_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: UnityEditorLifecycleState.Starting),
            CreatePingPayload());
        var logReader = new UnexpectedUnityLogReader("Accepted starting ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-starting")),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(UnityEditorLifecycleState.Starting, result.LifecycleObservation!.State.LifecycleState);
        Assert.False(UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsDomainReloading_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: UnityEditorLifecycleState.DomainReloading),
            CreatePingPayload());
        var logReader = new UnexpectedUnityLogReader("Accepted domain reload ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-domain-reloading")),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(UnityEditorLifecycleState.DomainReloading, result.LifecycleObservation!.State.LifecycleState);
        Assert.False(UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsCompiling_ReturnsReadyWithLifecycleSnapshot ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(
            lifecycleState: UnityEditorLifecycleState.Compiling));
        var logReader = new UnexpectedUnityLogReader("Compiling lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-compiling")),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(UnityEditorLifecycleState.Compiling, result.LifecycleObservation!.State.LifecycleState);
        Assert.False(UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityEditorLifecycleState.PlayMode, UnityEditorBlockingReason.PlayMode)]
    [InlineData(UnityEditorLifecycleState.ModalBlocked, UnityEditorBlockingReason.ModalDialog)]
    [InlineData(UnityEditorLifecycleState.SafeMode, UnityEditorBlockingReason.SafeMode)]
    [InlineData(UnityEditorLifecycleState.ShuttingDown, UnityEditorBlockingReason.Shutdown)]
    public async Task WaitUntilReady_WhenPingReportsNonReadyLifecycleState_ReturnsReadyWithLifecycleSnapshot (
        UnityEditorLifecycleState lifecycleState,
        UnityEditorBlockingReason blockingReason)
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            UnityEditorObservationTestFactory.Create(lifecycleState));
        var logReader = new UnexpectedUnityLogReader("Non-ready lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                ProjectFingerprintTestFactory.Create(
                    $"fingerprint-readiness-{TextVocabulary.GetText(lifecycleState)}")),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(lifecycleState, result.LifecycleObservation!.State.LifecycleState);
        Assert.Equal(
            blockingReason,
            UnityEditorLifecycleSemantics.ResolveBlockingReason(result.LifecycleObservation.State.LifecycleState));
        Assert.False(UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }
}
