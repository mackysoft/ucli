using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class TestProfileInitCliOutputContractTests
{
    private const string UcliDirectoryName = ".ucli";

    private const string TestProfileFileName = "test.profile.json";

    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithDefaultOutputPath_CreatesTemplateAndReturnsSuccessJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-profile-init-default");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        var expectedProfilePath = Path.Combine(workingDirectoryPath, UcliDirectoryName, TestProfileFileName);

        var result = await RunTestProfileInitAsync(workingDirectory: workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CliContractAssertions.AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.TestProfileInit,
            status: CliProtocol.StatusOk,
            exitCode: (int)CliExitCode.Success);
        CliContractAssertions.AssertNoErrors(outputJson.RootElement);
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

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("profiles/custom-profile", "profiles/custom-profile.json")]
    [InlineData("profiles/custom-profile.json", "profiles/custom-profile.json")]
    [InlineData("profiles/custom-profile.txt", "profiles/custom-profile.txt.json")]
    public async Task WithOutputPath_NormalizesJsonExtension (
        string outputPath,
        string expectedRelativePath)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-profile-init-output");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        var expectedProfilePath = Path.Combine(
            workingDirectoryPath,
            expectedRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var result = await RunTestProfileInitAsync(
            workingDirectory: workingDirectoryPath,
            outputPath: outputPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CliContractAssertions.AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.TestProfileInit,
            status: CliProtocol.StatusOk,
            exitCode: (int)CliExitCode.Success);
        CliContractAssertions.AssertNoErrors(outputJson.RootElement);
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
    public async Task WithoutForce_WhenTargetFileExists_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-profile-init-existing-no-force");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        var existingProfilePath = scope.WriteFile(Path.Combine("workspace", "profiles", "existing-profile.json"), "{\"legacy\":true}");

        var result = await RunTestProfileInitAsync(
            workingDirectory: workingDirectoryPath,
            outputPath: existingProfilePath,
            force: false);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CliContractAssertions.AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.TestProfileInit,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CliContractAssertions.AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);

        Assert.Equal("{\"legacy\":true}", File.ReadAllText(existingProfilePath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithForce_WhenTargetFileExists_OverwritesTemplate ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-profile-init-existing-force");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        var existingProfilePath = scope.WriteFile(Path.Combine("workspace", "profiles", "existing-profile.json"), "{\"legacy\":true}");

        var result = await RunTestProfileInitAsync(
            workingDirectory: workingDirectoryPath,
            outputPath: existingProfilePath,
            force: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CliContractAssertions.AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.TestProfileInit,
            status: CliProtocol.StatusOk,
            exitCode: (int)CliExitCode.Success);
        CliContractAssertions.AssertNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasValueKind("profilePath", JsonValueKind.String));

        var actualProfilePath = outputJson.RootElement.GetProperty("payload").GetProperty("profilePath").GetString();
        Assert.False(string.IsNullOrWhiteSpace(actualProfilePath));
        FileSystemAssert.ForPath(actualProfilePath!)
            .IsRooted()
            .EqualsNormalized(existingProfilePath)
            .Exists();
        AssertDefaultTestProfileValues(existingProfilePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithDirectoryOutputPath_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-profile-init-directory-path");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        var directoryPath = scope.CreateDirectory(Path.Combine("workspace", "existing-directory.json"));

        var result = await RunTestProfileInitAsync(
            workingDirectory: workingDirectoryPath,
            outputPath: directoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CliContractAssertions.AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.TestProfileInit,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CliContractAssertions.AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("profiles/")]
    [InlineData("profiles\\")]
    public async Task WithDirectoryStyleOutputPath_ReturnsInvalidArgumentError (string outputPath)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-profile-init-directory-style-output");
        var workingDirectoryPath = scope.CreateDirectory("workspace");

        var result = await RunTestProfileInitAsync(
            workingDirectory: workingDirectoryPath,
            outputPath: outputPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CliContractAssertions.AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.TestProfileInit,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CliContractAssertions.AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliToolRunner.RunAsync(
            UcliCommandNames.Test,
            UcliCommandNames.Profile,
            UcliCommandNames.InitSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CliContractAssertions.AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.TestProfileInit,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CliContractAssertions.AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    private static async Task<CommandExecutionResult> RunTestProfileInitAsync (
        string? workingDirectory = null,
        string? outputPath = null,
        bool force = false)
    {
        var args = new List<string>
        {
            UcliCommandNames.Test,
            UcliCommandNames.Profile,
            UcliCommandNames.InitSubcommand,
        };

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            args.Add(UcliContractConstants.CliOption.OutputPath);
            args.Add(outputPath);
        }

        if (force)
        {
            args.Add(UcliContractConstants.CliOption.Force);
        }

        return string.IsNullOrWhiteSpace(workingDirectory)
            ? await CliToolRunner.RunAsync(args.ToArray())
            : await CliToolRunner.RunWithWorkingDirectoryAsync(workingDirectory, args.ToArray());
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
            .IsNull("buildTarget")
            .IsNull("testFilter")
            .HasArrayLength("testCategories", 0)
            .HasArrayLength("assemblyNames", 0)
            .IsNull("testSettingsPath")
            .HasString("outputDir", UcliContractConstants.TestProfile.OutputDir)
            .HasInt32("timeoutSeconds", UcliContractConstants.TestProfile.TimeoutSeconds);
    }
}
