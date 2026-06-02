using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartupBlockedProcessPolicyResolverTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, "gui", "user", false, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, "gui", "cli", true, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Keep, "gui", "cli", true, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, "gui", "cli", true, 1234, true, DaemonStartupProcessAction.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, "gui", "cli", false, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, "batchmode", "cli", true, 1234, true, DaemonStartupProcessAction.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, "batchmode", "cli", true, 1234, true, DaemonStartupProcessAction.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, "batchmode", "cli", true, null, false, DaemonStartupProcessAction.None)]
    public void Resolve_ReturnsExpectedProcessPolicy (
        DaemonStartupBlockedProcessPolicy policy,
        string editorMode,
        string ownerKind,
        bool canShutdownProcess,
        int? processId,
        bool expectedShouldTerminate,
        DaemonStartupProcessAction expectedProcessActionWhenNotTerminated)
    {
        var result = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            policy,
            editorMode,
            ownerKind,
            canShutdownProcess,
            processId);

        Assert.Equal(expectedShouldTerminate, result.ShouldTerminateProcess);
        Assert.Equal(
            ContractLiteralCodec.ToValue(expectedProcessActionWhenNotTerminated),
            result.ProcessActionWhenNotTerminated);
    }
}
