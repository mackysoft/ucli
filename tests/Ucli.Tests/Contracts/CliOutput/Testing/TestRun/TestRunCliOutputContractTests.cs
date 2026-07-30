using System.Text.Json;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedTestRunServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

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
        AssertCommandErrorPayloadHasNoRunContext(outputJson.RootElement);
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
        AssertCommandErrorPayloadHasNoRunContext(outputJson.RootElement);
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

    private static void AssertCommandErrorPayloadHasNoRunContext (JsonElement root)
    {
        var payload = root.GetProperty("payload");
        Assert.Equal(
            TextVocabulary.GetText(CommandErrorPayloadKind.Detailed),
            payload.GetProperty("payloadKind").GetString());
        Assert.Equal(
            TextVocabulary.GetText(TestRunErrorKind.InvalidInput),
            payload.GetProperty("errorKind").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("run").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("startupFailure").ValueKind);
    }

}
