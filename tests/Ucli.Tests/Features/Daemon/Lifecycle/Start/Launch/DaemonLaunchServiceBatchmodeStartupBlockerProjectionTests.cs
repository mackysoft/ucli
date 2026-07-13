namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeStartupBlockerTestSupport;

public sealed class DaemonLaunchServiceBatchmodeStartupBlockerProjectionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeReturnsClassifiedBlocker_PreservesStartupClassification ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-probe-classified-blocker"),
            processId: 7778);

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.Error!.Code);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked), result.Startup!.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile), result.Startup.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix), result.Startup.RetryDisposition);
        Assert.Equal("batchmode", result.Startup.EditorMode);
        Assert.Equal("cli", result.Startup.OwnerKind);
        Assert.Equal(scenario.ProcessId, result.Startup.ProcessId);
        Assert.Equal(scenario.ProcessStartedAtUtc, result.Startup.StartedAtUtc);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), result.Startup.ProcessAction);
        Assert.NotNull(result.Startup.ArtifactPath);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Diagnosis!.Reason);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            scenario.CompensationService,
            scenario.Context,
            processId: scenario.ProcessId,
            processStartedAtUtc: scenario.ProcessStartedAtUtc);
        Assert.Equal(
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
            DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(scenario.LaunchAttemptStore, scenario.Context).StartupStatus);
        Assert.Equal(
            ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
            DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(scenario.LaunchAttemptStore, scenario.Context).StartupBlockingReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeReturnsClassifiedBlockerAndKeepPolicy_PreservesProcess ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            ProjectFingerprintTestFactory.Create("fingerprint-probe-classified-blocker-keep"),
            processId: 7779);

        var result = await scenario.LaunchAsync(DaemonStartupBlockedProcessPolicy.Keep);

        DaemonLaunchInvocationAssert.StartupFailureKeptProcessWithoutCompensation(
            result,
            scenario.CompensationService);
        Assert.Equal(
            ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept),
            DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(scenario.LaunchAttemptStore, scenario.Context).ProcessAction);
    }
}
