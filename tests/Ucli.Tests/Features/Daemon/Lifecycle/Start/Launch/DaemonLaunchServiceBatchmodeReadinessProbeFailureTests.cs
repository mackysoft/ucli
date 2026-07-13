namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeReadinessFailureTestSupport;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceBatchmodeReadinessProbeFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeFails_RunsCompensationAndReturnsProbeFailure ()
    {
        var probeError = ExecutionError.Timeout("probe failed");
        var scenario = CreateScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-probe-fail"),
            probeError,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore());

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(probeError, result.Error);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            scenario.CompensationService,
            scenario.Context,
            scenario.ProcessId,
            scenario.ProcessStartedAtUtc,
            timeout: TimeSpan.FromSeconds(10));
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(scenario.DiagnosisStore, scenario.Context);
        Assert.Equal(scenario.ProcessStartedAtUtc, diagnosis.ProcessStartedAtUtc);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.NotNull(result.Startup);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LaunchAttemptRecordedAndPrunedFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Timeout,
            DaemonStartupProcessAction.Terminated);
        Assert.Equal(DaemonStartupStatus.Timeout, launchAttempt.StartupStatus);
        Assert.Equal(DaemonStartupProcessAction.Terminated, launchAttempt.ProcessAction);
        Assert.Equal(scenario.ProcessStartedAtUtc, launchAttempt.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCanceledDuringReadinessProbe_RunsCompensationThenThrows ()
    {
        using var cancellationSource = new CancellationTokenSource();
        var scenario = CreateScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-launch-cancel-during-readiness"),
            ExecutionError.Timeout("probe failed"));
        scenario.ReadinessProbe.OnWaitUntilReady = cancellationSource.Cancel;
        scenario.ReadinessProbe.NextException = new OperationCanceledException(cancellationSource.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                scenario.LaunchAsync(cancellationSource.Token).AsTask(),
                "Canceled daemon launch result",
                AsyncWaitTimeout);
        });

        Assert.True(cancellationSource.IsCancellationRequested);
        DaemonLaunchInvocationAssert.LaunchCompensationAttemptedWithoutDiagnosisWrite(
            scenario.CompensationService,
            scenario.DiagnosisStore,
            scenario.Context,
            scenario.ProcessId,
            scenario.ProcessStartedAtUtc,
            timeout: TimeSpan.FromSeconds(10));
    }
}
