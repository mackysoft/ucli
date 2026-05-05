using MackySoft.Ucli.Features.Daemon.Supervisor.Invocation;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SystemdRunSupervisorProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArguments_AppendsInternalSupervisorInvocationArguments ()
    {
        const string repositoryRoot = "/repo";
        const string unitName = "mackysoft-ucli-supervisor-test";
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var arguments = SystemdRunSupervisorProcessLauncher.BuildArguments(
            repositoryRoot,
            unitName,
            launchCommand);

        Assert.Equal(
            [
                "--user",
                "--quiet",
                "--collect",
                "--unit",
                unitName,
                "--working-directory",
                repositoryRoot,
                "ucli",
                "--base",
                SupervisorInvocationArguments.InternalServeFlag,
                SupervisorInvocationArguments.RepositoryRootOption,
                repositoryRoot,
            ],
            arguments);
    }
}
