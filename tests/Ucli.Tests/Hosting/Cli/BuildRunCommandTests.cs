using System.Text.RegularExpressions;
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

    private static readonly string[] RemovedArtifactNames =
    [
        "build-summary.json",
        "profile-snapshot.json",
        "lifecycle.json",
        "manifest.json",
    ];

    private static readonly string[] UndocumentedFeatureNames =
    [
        "build profile init",
        "output path override",
        "retry reconciliation",
        "dirty state",
        "project mutation audit",
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithHelpOutput_DescribesCurrentBuildArtifactsOnly ()
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

        foreach (var artifactName in RemovedArtifactNames)
        {
            Assert.DoesNotMatch(CreateStandaloneArtifactNamePattern(artifactName), result.StdOut);
        }

        Assert.DoesNotContain("sha256:", result.StdOut, StringComparison.Ordinal);
        foreach (var featureName in UndocumentedFeatureNames)
        {
            Assert.DoesNotContain(featureName, result.StdOut, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static Regex CreateStandaloneArtifactNamePattern (string artifactName)
    {
        return new Regex(
            "(?<![A-Za-z0-9-])" + Regex.Escape(artifactName) + "(?![A-Za-z0-9-])",
            RegexOptions.CultureInvariant);
    }
}
