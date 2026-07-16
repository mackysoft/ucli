namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliProcessContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonVersionOption_ReturnsBuiltAssemblyVersionAndSuccessExitCode ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            "--version");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+([-\+].*)?$", result.StdOut.Trim());
        Assert.Equal(string.Empty, result.StdErr);
    }
}
