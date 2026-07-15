using System.Text.Json;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class TestProfileInitCliOutputContractTests
{
    private const string TestProfileFileName = "test.profile.json";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithDefaultOutputPath_CreatesTemplateAndReturnsSuccessJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-profile-init-default");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        var expectedProfilePath = Path.Combine(workingDirectoryPath, TestProfileFileName);

        var result = await RunTestProfileInitAsync(workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestProfileInit);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasValueKind("profilePath", JsonValueKind.String));

        var actualProfilePath = outputJson.RootElement.GetProperty("payload").GetProperty("profilePath").GetString();
        Assert.False(string.IsNullOrWhiteSpace(actualProfilePath));
        FileSystemAssert.ForPath(actualProfilePath!)
            .IsRooted()
            .EqualsNormalized(expectedProfilePath)
            .Exists();
        AssertDefaultTestProfileValues(expectedProfilePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Test,
            UcliCommandNames.Profile,
            UcliCommandNames.InitSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestProfileInit);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Unknown);
    }

    private static Task<CommandExecutionResult> RunTestProfileInitAsync (string workingDirectory)
    {
        return CliInProcessRunner.RunCommandWithWorkingDirectoryAsync(
            workingDirectory,
            UcliCommandNames.Test,
            UcliCommandNames.Profile,
            UcliCommandNames.InitSubcommand);
    }

    private static void AssertDefaultTestProfileValues (string profilePath)
    {
        using var profileJson = JsonDocument.Parse(File.ReadAllText(profilePath));
        JsonAssert.For(profileJson.RootElement)
            .HasInt32("schemaVersion", UcliContractConstants.TestProfile.SchemaVersion)
            .HasString("projectPath", UcliContractConstants.TestProfile.ProjectPath)
            .IsNull("unityVersion")
            .IsNull("unityEditorPath")
            .HasString("testPlatform", UcliContractConstants.TestProfile.TestPlatformEditMode)
            .IsNull("testFilter")
            .HasArrayLength("testCategories", 0)
            .HasArrayLength("assemblyNames", 0)
            .HasInt32("timeout", UcliContractConstants.TestProfile.TimeoutMilliseconds);
        Assert.False(profileJson.RootElement.TryGetProperty("testSettingsPath", out _));
    }
}
