namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceBatchmodeStartupBlockerTestSupport;

public sealed class DaemonLaunchServiceBatchmodeStartupBlockerEvidenceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerTerminates_CompensatesBeforeSupplementalEvidence ()
    {
        var scenario = CreateClassifiedBlockerScenario(
            "fingerprint-probe-classified-blocker-order",
            processId: 7780);
        var sequence = new List<string>();
        scenario.DiagnosisStore.OnWrite = _ => sequence.Add("diagnosis");
        scenario.LaunchAttemptStore.OnWrite = attempt => sequence.Add($"launchAttempt:{attempt.ProcessAction}");
        scenario.CompensationService.OnCleanup = () => sequence.Add("cleanup");

        var result = await scenario.LaunchAsync();

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), result.Startup!.ProcessAction);
        var cleanupIndex = sequence.IndexOf("cleanup");
        Assert.Equal(0, cleanupIndex);
        Assert.Contains(
            sequence.Skip(1),
            value => string.Equals(
                value,
                $"launchAttempt:{ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated)}",
                StringComparison.Ordinal));
        Assert.Contains("diagnosis", sequence.Skip(1));
        Assert.Single(scenario.LaunchAttemptStore.WriteInvocations);
        Assert.Equal(
            ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated),
            DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(scenario.LaunchAttemptStore, scenario.Context).ProcessAction);
    }
}
