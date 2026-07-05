using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchServiceGuiStartupObserverFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupWaitIsCanceled_RunsCompensationAndRethrows ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-launch-cancel");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7654, processStartedAtUtc),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            Handler = _ =>
            {
                cancellationTokenSource.Cancel();
                return ValueTask.FromCanceled<DaemonGuiStartupObservationResult>(cancellationTokenSource.Token);
            },
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Gui,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    cancellationToken: cancellationTokenSource.Token)
                .AsTask());

        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 7654,
            processStartedAtUtc: processStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFails_RunsCompensationAndReturnsFailure ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-launch-observer-fail");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var startupError = ExecutionError.InternalError("observer failed");
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(8765, processStartedAtUtc),
        };
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(startupError),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(startupError, result.Error);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 8765,
            processStartedAtUtc: processStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), result.Startup!.ProcessAction);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed), launchAttempt.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), launchAttempt.ProcessAction);
    }
}
