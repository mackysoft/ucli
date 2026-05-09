using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.UnityIntegration.Ipc.Process;

public sealed class UnityGuiEditorProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArgumentTokens_IncludesGuiBootstrapArgumentsAndOmitsBatchmodeArguments ()
    {
        var tokens = UnityGuiEditorProcessLauncher.BuildArgumentTokens(
            "/repo/UnityProject",
            "/repo/.ucli/logs/unity.log",
            new IpcGuiBootstrapArguments(
                OwnerProcessId: 123,
                CanShutdownProcess: true));

        Assert.DoesNotContain("-batchmode", tokens);
        Assert.DoesNotContain("-nographics", tokens);
        Assert.Equal(
            [
                "-projectPath",
                "/repo/UnityProject",
                "-logFile",
                "/repo/.ucli/logs/unity.log",
                IpcGuiBootstrapArgumentNames.Target,
                IpcGuiBootstrapTargetValues.Daemon,
                IpcGuiBootstrapArgumentNames.OwnerProcessId,
                "123",
                IpcGuiBootstrapArgumentNames.CanShutdownProcess,
                "true",
            ],
            tokens);
    }
}
