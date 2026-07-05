using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedTestRunServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    private static readonly string[][] FrameworkHelpArgumentCases =
    [
        [],
        ["unknown"],
        [UcliCommandNames.Profile],
        [UcliCommandNames.Profile, "unknown"],
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TestFramework_WithMissingOrUnknownSubcommand_ReturnsHelpOutput ()
    {
        foreach (var arguments in FrameworkHelpArgumentCases)
        {
            var result = await CliInProcessRunner.RunCommandAsync([UcliCommandNames.Test, .. arguments]);

            AssertFrameworkHelpOutput(result);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Test,
            UcliCommandNames.RunSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithMissingProjectPath_ReturnsInvalidInputErrorContract ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-run-missing-project");
        var missingProjectPath = scope.GetPath("workspace/UnityProject");

        var result = await RunTestRunCommandAsync(
            projectPath: missingProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: ProjectContextErrorCodes.ProjectPathNotFound);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .IsNull("result")
                .HasString("errorKind", "invalidInput")
                .IsNull("runId")
                .IsNull("artifactsDir")
                .IsNull("summaryJsonPath"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Test,
            UcliCommandNames.RunSubcommand,
            UcliContractConstants.CliOption.FailFast,
            "--format",
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, UcliContractConstants.CliOption.FailFast);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithInvalidMode_ReturnsTestRunInvalidInputEnvelope ()
    {
        var result = await RunTestRunCommandAsync(
            executionMode: "unsupported");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("test-run", "invalid-mode.json"), result.StdOut);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WithWhitespaceTestPlatform_ReturnsTestRunInvalidInputEnvelope ()
    {
        var result = await RunTestRunCommandAsync(
            testPlatform: " ");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .IsNull("result")
                .HasString("errorKind", "invalidInput")
                .IsNull("runId")
                .IsNull("artifactsDir")
                .IsNull("summaryJsonPath"));
    }

    private static Task<CommandExecutionResult> RunTestRunCommandAsync (
        string? projectPath = null,
        string? executionMode = null,
        string? testPlatform = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<TestRunCommand>(
                    SharedTestRunServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .RunAsync(
                    projectPath: projectPath,
                    executionMode: executionMode,
                    testPlatform: testPlatform,
                    cancellationToken: CancellationToken.None));
    }

    private static void AssertFrameworkHelpOutput (CommandExecutionResult result)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("test run", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("test profile init", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("\"protocolVersion\"", result.StdOut, StringComparison.Ordinal);
    }
}
