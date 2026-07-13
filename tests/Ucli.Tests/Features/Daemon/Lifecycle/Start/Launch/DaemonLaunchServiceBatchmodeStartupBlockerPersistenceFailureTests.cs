using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeStartupBlockerTestSupport;

public sealed class DaemonLaunchServiceBatchmodeStartupBlockerPersistenceFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerDiagnosisWriteFails_PreservesPrimaryBlockerAndStillCompensates ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            "fingerprint-probe-classified-blocker-diagnosis-fail",
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
        DaemonLaunchAttemptStoreAssert.LaunchAttemptEvidenceBeforeAndAfterCompensationFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            DaemonStartupProcessAction.Terminated);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerLaunchAttemptWriteFails_PreservesPrimaryBlockerAndStillCompensates ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            "fingerprint-probe-classified-blocker-artifact-fail",
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
        DaemonLaunchAttemptStoreAssert.LaunchAttemptEvidenceBeforeAndAfterCompensationWithoutPruneFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            DaemonStartupProcessAction.Terminated);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerFinalLaunchAttemptWriteFails_PreservesPrimaryBlockerAndReportsArtifactError ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            "fingerprint-probe-classified-blocker-final-artifact-fail",
            processId: 7784);
        scenario.LaunchAttemptStore.WriteResults.Enqueue(DaemonLaunchAttemptStoreOperationResult.Success());
        scenario.LaunchAttemptStore.WriteResults.Enqueue(
            DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError("final artifact failed")));

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("StartupError=Unity scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Contains("ArtifactError=final artifact failed", error.Message, StringComparison.Ordinal);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            scenario.CompensationService,
            scenario.Context,
            processId: scenario.ProcessId,
            processStartedAtUtc: scenario.ProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.Terminated, result.Startup!.ProcessAction);
        DaemonLaunchAttemptStoreAssert.LaunchAttemptEvidenceBeforeAndAfterCompensationFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            DaemonStartupProcessAction.Terminated);
    }
}
