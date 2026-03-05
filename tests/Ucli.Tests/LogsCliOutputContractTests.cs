using MackySoft.Tests;
using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class LogsCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Logs_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Logs);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Logs,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LogsDaemon_WithInvalidInput_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Logs,
            UcliCommandNames.Daemon,
            "--queryTarget",
            "stack");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsDaemon,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LogsUnity_ReturnsCommandNotImplementedError ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Logs,
            UcliCommandNames.UnitySubcommand);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsUnity,
            status: "error",
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "COMMAND_NOT_IMPLEMENTED");
    }
}