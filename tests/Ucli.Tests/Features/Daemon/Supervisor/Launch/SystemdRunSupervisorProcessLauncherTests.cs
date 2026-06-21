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
                ..SupervisorInvocationArguments.Build(repositoryRoot),
            ],
            arguments);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArguments_WhenEnvironmentVariablesAreSpecified_AppendsSetEnvironmentOptionsBeforeCommand ()
    {
        const string repositoryRoot = "/repo";
        const string unitName = "mackysoft-ucli-supervisor-test";
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var arguments = SystemdRunSupervisorProcessLauncher.BuildArguments(
            repositoryRoot,
            unitName,
            launchCommand,
            [
                new KeyValuePair<string,string>("UCLI_RUNTIME_TRACE_DIR", "/tmp/ucli-trace"),
                new KeyValuePair<string,string>("UCLI_RUNTIME_TRACE_SESSION", "before"),
            ]);

        Assert.Equal(
            [
                "--user",
                "--quiet",
                "--collect",
                "--unit",
                unitName,
                "--working-directory",
                repositoryRoot,
                "--setenv",
                "UCLI_RUNTIME_TRACE_DIR=/tmp/ucli-trace",
                "--setenv",
                "UCLI_RUNTIME_TRACE_SESSION=before",
                "ucli",
                "--base",
                ..SupervisorInvocationArguments.Build(repositoryRoot),
            ],
            arguments);
    }
}
