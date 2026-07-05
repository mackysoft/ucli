using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandParserTests
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
    public async Task BuildRun_WithHelpOutput_ExposesPublicOptionsAndDescribesArtifacts ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(UcliContractConstants.CliOption.ProfilePath, result.StdOut, StringComparison.Ordinal);
        Assert.Contains(UcliContractConstants.CliOption.ProjectPath, result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain(UcliContractConstants.CliOption.BuildTarget, result.StdOut, StringComparison.Ordinal);
        Assert.Contains("-p", result.StdOut, StringComparison.Ordinal);
        Assert.Contains(UcliContractConstants.CliOption.Mode, result.StdOut, StringComparison.Ordinal);
        Assert.Contains(UcliContractConstants.CliOption.Timeout, result.StdOut, StringComparison.Ordinal);
        Assert.Contains("--format", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain(UcliContractConstants.CliOption.OutputPath, result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("profile init", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("allowDirty", result.StdOut, StringComparison.Ordinal);

        foreach (var artifactName in CurrentArtifactNames)
        {
            Assert.Contains(artifactName, result.StdOut, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(UcliContractConstants.CliOption.ProjectPath)]
    [InlineData("-p")]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithPublicOptions_ReachesProjectPathValidation (string projectPathOption)
    {
        using var scope = TestDirectories.CreateTempScope(
            nameof(BuildRunCommandParserTests),
            nameof(BuildRun_WithPublicOptions_ReachesProjectPathValidation));
        var missingProfilePath = scope.GetPath("missing-build-profile.json");
        var missingProjectPath = scope.GetPath("missing-unity-project");

        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            UcliContractConstants.CliOption.ProfilePath,
            missingProfilePath,
            projectPathOption,
            missingProjectPath,
            UcliContractConstants.CliOption.Mode,
            "daemon",
            UcliContractConstants.CliOption.Timeout,
            "1",
            "--format",
            "json");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ProjectContextErrorCodes.ProjectPathNotFound);
    }

    [Theory]
    [InlineData(UcliContractConstants.CliOption.OutputPath)]
    [InlineData(UcliContractConstants.CliOption.BuildTarget)]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithUnspecifiedBuildOption_ReturnsJsonInvalidArgument (string option)
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            option,
            "/tmp/output");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, option);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.BuildRun);
    }
}
