using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeStartupBlockerTestSupport;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceBatchmodeStartupBlockerPersistenceFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerDiagnosisWriteFails_PreservesPrimaryBlockerAndStillCompensates ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-probe-classified-blocker-diagnosis-fail"),
            processId: 7781);
        scenario.DiagnosisStore.WriteResult =
            DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("diagnosis failed"));

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("StartupError=Unity scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Contains("DiagnosisError=diagnosis failed", error.Message, StringComparison.Ordinal);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            scenario.CompensationService,
            scenario.Context,
            processId: scenario.ProcessId,
            processStartedAtUtc: scenario.ProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.Terminated, result.Startup!.ProcessAction);
        DaemonLaunchAttemptStoreAssert.SingleLaunchAttemptRecordedAndPrunedFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Blocked,
            DaemonStartupProcessAction.Terminated);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerLaunchAttemptWriteFails_PreservesPrimaryBlockerAndStillCompensates ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-probe-classified-blocker-artifact-fail"),
            processId: 7782);
        scenario.LaunchAttemptStore.WriteResult =
            DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError("artifact failed"));

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("StartupError=Unity scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Contains("ArtifactError=artifact failed", error.Message, StringComparison.Ordinal);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            scenario.CompensationService,
            scenario.Context,
            processId: scenario.ProcessId,
            processStartedAtUtc: scenario.ProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.Terminated, result.Startup!.ProcessAction);
        DaemonLaunchAttemptStoreAssert.SingleLaunchAttemptRecordedWithoutPruneFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Blocked,
            DaemonStartupProcessAction.Terminated);
    }
}
