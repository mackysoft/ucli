using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeStartupBlockerTestSupport;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceBatchmodeStartupBlockerCleanupFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerLaunchAttemptWriteAndCleanupFail_ReportsBothSecondaryErrors ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-probe-classified-blocker-final-artifact-cleanup-fail"),
            processId: 7785);
        scenario.LaunchAttemptStore.WriteResult =
            DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError("artifact failed"));
        scenario.CompensationService.NextResult =
            DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError("cleanup failed"));

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("StartupError=Unity scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Contains("ArtifactError=artifact failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.Unknown, result.Startup!.ProcessAction);
        DaemonLaunchAttemptStoreAssert.SingleLaunchAttemptRecordedWithoutPruneFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Blocked,
            DaemonStartupProcessAction.Unknown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerCleanupFails_RecordsUnknownProcessAction ()
    {
        var primaryDiagnostic = new DaemonPrimaryDiagnostic(
            Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
            Code: "CS1002",
            File: "Assets/Foo.cs",
            Line: 42,
            Column: 13,
            Message: "Semicolon expected");
        var scenario = CreateClassifiedBlockerScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-probe-classified-blocker-cleanup-fail"),
            processId: 7783,
            primaryDiagnostic);
        scenario.CompensationService.NextResult =
            DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError("cleanup failed"));

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("StartupError=Unity scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            scenario.CompensationService,
            scenario.Context,
            processId: scenario.ProcessId,
            processStartedAtUtc: scenario.ProcessStartedAtUtc);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(DaemonDiagnosisReason.UnityScriptCompilationFailed, result.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisStartupPhase.ScriptCompilation, result.Diagnosis.StartupPhase);
        Assert.Equal(DaemonDiagnosisActionRequired.FixCompileErrors, result.Diagnosis.ActionRequired);
        Assert.Equal(primaryDiagnostic, result.Diagnosis.PrimaryDiagnostic);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.Unknown, result.Startup!.ProcessAction);
        var finalLaunchAttempt = DaemonLaunchAttemptStoreAssert.SingleLaunchAttemptRecordedAndPrunedFor(
            scenario.LaunchAttemptStore,
            scenario.Context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Blocked,
            DaemonStartupProcessAction.Unknown);
        Assert.Equal(result.Diagnosis, finalLaunchAttempt.Diagnosis);
    }
}
