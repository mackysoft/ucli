namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using static DaemonStartupReadinessProbeTestSupport;

public sealed class DaemonStartupReadinessProbeLifecycleTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingSucceeds_ReturnsReadyWithoutLogInspection ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(canAcceptExecutionRequests: true));
        var logReader = new UnexpectedUnityLogReader("Ready ping success should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-success"),
            TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.Ready, result.LifecycleSnapshot!.LifecycleState);
        Assert.True(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsStarting_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: IpcEditorLifecycleState.Starting,
                canAcceptExecutionRequests: false),
            CreatePingPayload(canAcceptExecutionRequests: true));
        var logReader = new UnexpectedUnityLogReader("Accepted starting ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-starting"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.Starting, result.LifecycleSnapshot!.LifecycleState);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsDomainReloading_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: IpcEditorLifecycleState.DomainReloading,
                canAcceptExecutionRequests: false),
            CreatePingPayload(canAcceptExecutionRequests: true));
        var logReader = new UnexpectedUnityLogReader("Accepted domain reload ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-domain-reloading"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.DomainReloading, result.LifecycleSnapshot!.LifecycleState);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsCompiling_ReturnsReadyWithLifecycleSnapshot ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(
            lifecycleState: IpcEditorLifecycleState.Compiling,
            canAcceptExecutionRequests: false));
        var logReader = new UnexpectedUnityLogReader("Compiling lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-compiling"),
            TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleState.Compiling, result.LifecycleSnapshot!.LifecycleState);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
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
        var pingClient = new RecordingDaemonPingInfoClient(IpcPingResponseTestFactory.Create(
            lifecycleState: ContractLiteralCodec.ToValue(lifecycleState),
            blockingReason: ContractLiteralCodec.ToValue(blockingReason),
            canAcceptExecutionRequests: false));
        var logReader = new UnexpectedUnityLogReader("Non-ready lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                $"fingerprint-readiness-{ContractLiteralCodec.ToValue(lifecycleState)}"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(lifecycleState, result.LifecycleSnapshot!.LifecycleState);
        Assert.Equal(blockingReason, result.LifecycleSnapshot.BlockingReason);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }
}
