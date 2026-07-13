namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
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
        var probe = CreateProbe(pingClient, logReader, timeProvider: new ManualTimeProvider());

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-success")),
            TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.Ready, result.LifecycleObservation!.State.LifecycleState);
        Assert.True(IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsStarting_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: IpcEditorLifecycleState.Starting),
            CreatePingPayload());
        var logReader = new UnexpectedUnityLogReader("Accepted starting ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-starting")),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.Starting, result.LifecycleObservation!.State.LifecycleState);
        Assert.False(IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsDomainReloading_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: IpcEditorLifecycleState.DomainReloading),
            CreatePingPayload());
        var logReader = new UnexpectedUnityLogReader("Accepted domain reload ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-domain-reloading")),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.DomainReloading, result.LifecycleObservation!.State.LifecycleState);
        Assert.False(IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsCompiling_ReturnsReadyWithLifecycleSnapshot ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(
            lifecycleState: IpcEditorLifecycleState.Compiling));
        var logReader = new UnexpectedUnityLogReader("Compiling lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader, timeProvider: new ManualTimeProvider());

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-compiling")),
            TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.Compiling, result.LifecycleObservation!.State.LifecycleState);
        Assert.False(IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcEditorLifecycleState.PlayMode, IpcEditorBlockingReason.PlayMode)]
    [InlineData(IpcEditorLifecycleState.ModalBlocked, IpcEditorBlockingReason.ModalDialog)]
    [InlineData(IpcEditorLifecycleState.SafeMode, IpcEditorBlockingReason.SafeMode)]
    [InlineData(IpcEditorLifecycleState.ShuttingDown, IpcEditorBlockingReason.Shutdown)]
    public async Task WaitUntilReady_WhenPingReportsNonReadyLifecycleState_ReturnsReadyWithLifecycleSnapshot (
        IpcEditorLifecycleState lifecycleState,
        IpcEditorBlockingReason blockingReason)
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            IpcUnityEditorObservationTestFactory.Create(lifecycleState));
        var logReader = new UnexpectedUnityLogReader("Non-ready lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                ProjectFingerprintTestFactory.Create(
                    $"fingerprint-readiness-{ContractLiteralCodec.ToValue(lifecycleState)}")),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(lifecycleState, result.LifecycleObservation!.State.LifecycleState);
        Assert.Equal(
            blockingReason,
            IpcEditorLifecycleSemantics.ResolveBlockingReason(result.LifecycleObservation.State.LifecycleState));
        Assert.False(IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
    }
}
