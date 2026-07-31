using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartupBlockedProcessPolicyResolverTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, UnityEditorMode.Gui, DaemonSessionOwnerKind.User, false, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, UnityEditorMode.Gui, DaemonSessionOwnerKind.Cli, true, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Keep, UnityEditorMode.Gui, DaemonSessionOwnerKind.Cli, true, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, UnityEditorMode.Gui, DaemonSessionOwnerKind.Cli, true, 1234, true, DaemonStartupProcessAction.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, UnityEditorMode.Gui, DaemonSessionOwnerKind.Cli, false, 1234, false, DaemonStartupProcessAction.Kept)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, UnityEditorMode.Batchmode, DaemonSessionOwnerKind.Cli, true, 1234, true, DaemonStartupProcessAction.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, UnityEditorMode.Batchmode, DaemonSessionOwnerKind.Cli, true, 1234, true, DaemonStartupProcessAction.Unknown)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, UnityEditorMode.Batchmode, DaemonSessionOwnerKind.Cli, true, null, false, DaemonStartupProcessAction.None)]
    public void Resolve_ReturnsExpectedProcessPolicy (
        DaemonStartupBlockedProcessPolicy policy,
        UnityEditorMode editorMode,
        DaemonSessionOwnerKind ownerKind,
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
            expectedProcessActionWhenNotTerminated,
            result.ProcessActionWhenNotTerminated);
    }
}
