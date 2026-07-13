namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
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
        var probe = CreateProbe(pingClient, logReader, timeProvider: new ManualTimeProvider());

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-success")),
            TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, result.LifecycleSnapshot!.LifecycleState);
        Assert.True(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsStarting_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: IpcEditorLifecycleStateCodec.Starting,
                canAcceptExecutionRequests: false),
            CreatePingPayload(canAcceptExecutionRequests: true));
        var logReader = new UnexpectedUnityLogReader("Accepted starting ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-starting")),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleStateCodec.Starting, result.LifecycleSnapshot!.LifecycleState);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsDomainReloading_RetriesUntilExecutionIsAccepted ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(
                lifecycleState: IpcEditorLifecycleStateCodec.DomainReloading,
                canAcceptExecutionRequests: false),
            CreatePingPayload(canAcceptExecutionRequests: true));
        var logReader = new UnexpectedUnityLogReader("Accepted domain reload ping should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-domain-reloading")),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleStateCodec.DomainReloading, result.LifecycleSnapshot!.LifecycleState);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsCompiling_ReturnsReadyWithLifecycleSnapshot ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(
            lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
            canAcceptExecutionRequests: false));
        var logReader = new UnexpectedUnityLogReader("Compiling lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader, timeProvider: new ManualTimeProvider());

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-compiling")),
            TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, result.LifecycleSnapshot!.LifecycleState);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode)]
    [InlineData(IpcEditorLifecycleStateCodec.ModalBlocked, IpcEditorBlockingReasonCodec.ModalDialog)]
    [InlineData(IpcEditorLifecycleStateCodec.SafeMode, IpcEditorBlockingReasonCodec.SafeMode)]
    [InlineData(IpcEditorLifecycleStateCodec.ShuttingDown, IpcEditorBlockingReasonCodec.Shutdown)]
    public async Task WaitUntilReady_WhenPingReportsNonReadyLifecycleState_ReturnsReadyWithLifecycleSnapshot (
        string lifecycleState,
        string blockingReason)
    {
        var pingClient = new RecordingDaemonPingInfoClient(IpcPingResponseTestFactory.Create(
            lifecycleState: lifecycleState,
            blockingReason: blockingReason,
            canAcceptExecutionRequests: false));
        var logReader = new UnexpectedUnityLogReader("Non-ready lifecycle snapshot should not inspect the Unity log.");
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create($"fingerprint-readiness-{lifecycleState}")),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(lifecycleState, result.LifecycleSnapshot!.LifecycleState);
        Assert.Equal(blockingReason, result.LifecycleSnapshot.BlockingReason);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }
}
