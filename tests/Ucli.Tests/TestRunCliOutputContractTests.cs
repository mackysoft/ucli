using MackySoft.Tests;
using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Test,
            UcliCommandNames.RunSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.TestRun,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WithMissingProjectPath_ReturnsInvalidInputErrorContract ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "test-run-missing-project");
        var missingProjectPath = scope.GetPath("workspace/UnityProject");

        var result = await RunTestRun(projectPath: missingProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.TestRun,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
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

    private static Task<CommandExecutionResult> RunTestRun (string? projectPath = null)
    {
        var args = new List<string>
        {
            UcliCommandNames.Test,
            UcliCommandNames.RunSubcommand,
        };

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            args.Add(UcliContractConstants.CliOption.ProjectPath);
            args.Add(projectPath);
        }

        return CliProcessRunner.RunCommand(args.ToArray());
    }
}