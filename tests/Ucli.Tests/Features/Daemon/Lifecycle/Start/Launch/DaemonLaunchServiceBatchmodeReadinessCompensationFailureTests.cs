namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeReadinessFailureTestSupport;

public sealed class DaemonLaunchServiceBatchmodeReadinessCompensationFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisWriteFails_PreservesPrimaryTimeoutErrorKind ()
    {
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(
                ExecutionError.InternalError("diagnosis write failed")),
        };
        var scenario = CreateScenario(
            "fingerprint-diagnosis-write-timeout",
            ExecutionError.Timeout("probe failed"),
            diagnosisStore: diagnosisStore);

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("DiagnosisError=diagnosis write failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCleanupFails_PreservesPrimaryTimeoutErrorKind ()
    {
        var cleanupError = ExecutionError.InternalError("cleanup failed");
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(cleanupError),
        };
        var scenario = CreateScenario(
            "fingerprint-cleanup-timeout",
            ExecutionError.Timeout("probe failed"),
            compensationService: compensationService);

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
    }
}
