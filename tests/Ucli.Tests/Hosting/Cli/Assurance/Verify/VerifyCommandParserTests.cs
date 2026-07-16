namespace MackySoft.Ucli.Tests;

public sealed class VerifyCommandParserTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Verify_WithHelpOutput_IncludesProfilePathCamelCaseOption ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Verify,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(UcliContractConstants.CliOption.ProfilePath, result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Verify_WithProfilePathCamelCaseAlias_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Verify_WithProfilePathCamelCaseAlias_IsAcceptedByParser));
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var profilePath = Path.Combine(unityProjectPath, "verify-profile.json");
        await File.WriteAllTextAsync(
            profilePath,
            "{\"schemaVersion\":1,\"steps\":[]}",
            CancellationToken.None);

        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Verify,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Profile,
            "built-in:default",
            UcliContractConstants.CliOption.ProfilePath,
            profilePath);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.Verify);
    }
}
