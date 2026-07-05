namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeReadinessFailureTestSupport;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceBatchmodeReadinessArtifactFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAttemptWriteFails_PreservesPrimaryTimeoutFailureAndStartupObservation ()
    {
        var probeError = ExecutionError.Timeout("probe failed", ExecutionErrorCodes.IpcTimeout);
        var artifactError = ExecutionError.InternalError("artifact write failed");
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore
        {
            WriteResult = DaemonLaunchAttemptStoreOperationResult.Failure(artifactError),
        };
        var scenario = CreateScenario(
            "fingerprint-launch-attempt-write-fail",
            probeError,
            launchAttemptStore: launchAttemptStore);

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("ArtifactError=artifact write failed", error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Startup);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout), result.Startup!.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), result.Startup.ProcessAction);
        DaemonLaunchAttemptStoreAssert.SingleLaunchAttemptRecordedWithoutPruneFor(
            launchAttemptStore,
            scenario.Context,
            AssertStartupLaunchAttemptId(result.Startup),
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout),
            ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAttemptPruneFails_PreservesPrimaryTimeoutFailureAndStartupObservation ()
    {
        var probeError = ExecutionError.Timeout("probe failed", ExecutionErrorCodes.IpcTimeout);
        var artifactError = ExecutionError.InternalError("artifact prune failed");
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore
        {
            PruneResult = DaemonLaunchAttemptStoreOperationResult.Failure(artifactError),
        };
        var scenario = CreateScenario(
            "fingerprint-launch-attempt-prune-fail",
            probeError,
            processId: 7778,
            launchAttemptStore: launchAttemptStore);

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("ArtifactError=artifact prune failed", error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Startup);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout), result.Startup!.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), result.Startup.ProcessAction);
        DaemonLaunchAttemptStoreAssert.SingleLaunchAttemptRecordedAndPrunedFor(
            launchAttemptStore,
            scenario.Context,
            AssertStartupLaunchAttemptId(result.Startup),
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout),
            ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated));
    }
}
