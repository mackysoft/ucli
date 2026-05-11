using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartupBlockedProcessPolicyResolverTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, DaemonEditorModeValues.Gui, DaemonSessionOwnerKindValues.User, false, 1234, false, DaemonStartupProcessActionValues.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, DaemonEditorModeValues.Gui, DaemonSessionOwnerKindValues.Cli, true, 1234, false, DaemonStartupProcessActionValues.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Keep, DaemonEditorModeValues.Gui, DaemonSessionOwnerKindValues.Cli, true, 1234, false, DaemonStartupProcessActionValues.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, DaemonEditorModeValues.Gui, DaemonSessionOwnerKindValues.Cli, true, 1234, true, DaemonStartupProcessActionValues.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, DaemonEditorModeValues.Gui, DaemonSessionOwnerKindValues.Cli, false, 1234, false, DaemonStartupProcessActionValues.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, DaemonEditorModeValues.Batchmode, DaemonSessionOwnerKindValues.Cli, true, 1234, true, DaemonStartupProcessActionValues.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, DaemonEditorModeValues.Batchmode, DaemonSessionOwnerKindValues.Cli, true, 1234, true, DaemonStartupProcessActionValues.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, DaemonEditorModeValues.Batchmode, DaemonSessionOwnerKindValues.Cli, true, null, false, DaemonStartupProcessActionValues.None)]
    public void Resolve_ReturnsExpectedProcessPolicy (
        DaemonStartupBlockedProcessPolicy policy,
        string editorMode,
        string ownerKind,
        bool canShutdownProcess,
        int? processId,
        bool expectedShouldTerminate,
        string expectedProcessActionWhenNotTerminated)
    {
        var result = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            policy,
            editorMode,
            ownerKind,
            canShutdownProcess,
            processId);

        Assert.Equal(expectedShouldTerminate, result.ShouldTerminateProcess);
        Assert.Equal(expectedProcessActionWhenNotTerminated, result.ProcessActionWhenNotTerminated);
    }
}
