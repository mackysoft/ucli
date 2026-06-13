using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandTests
{
    private static readonly string[] CurrentArtifactNames =
    [
        "build.json",
        "build-report.json",
        "build.log",
        "output-manifest.json",
        "output/",
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithHelpOutput_DescribesBuildArtifacts ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        foreach (var artifactName in CurrentArtifactNames)
        {
            Assert.Contains(artifactName, result.StdOut, StringComparison.Ordinal);
        }
    }
}
