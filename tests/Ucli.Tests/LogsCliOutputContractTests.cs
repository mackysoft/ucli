using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests;

public sealed class LogsCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Logs_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Logs);

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
    public async Task LogsDaemonRead_WithInvalidInput_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            UcliCommandNames.Daemon,
            UcliCommandNames.ReadSubcommand,
            "--queryTarget",
            "stack");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsDaemonRead,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(
            "queryTarget 'stack' is not supported for daemon logs.",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LogsUnityRead_WithInvalidStackTrace_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            UcliCommandNames.UnitySubcommand,
            UcliCommandNames.ReadSubcommand,
            "--tail",
            "1",
            "--stack-trace",
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsUnityRead,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(
            "stackTrace must be one of: none, error, all. Actual: unsupported.",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LogsUnityClear_WithReadOption_ReturnsJsonEnvelopeErrorForClearCommand ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            UcliCommandNames.UnitySubcommand,
            UcliCommandNames.ClearSubcommand,
            "--tail",
            "1");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsUnityClear,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LogsUnityRead_WithStreamOption_UsesStreamModeValidation ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            UcliCommandNames.UnitySubcommand,
            UcliCommandNames.ReadSubcommand,
            "--stream",
            "--poll-interval-milliseconds",
            "49");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsUnityRead,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(
            "pollIntervalMilliseconds must be between 50 and 60000. Actual: 49.",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.UnitySubcommand, "Subcommand is required for command 'logs unity'. Supported subcommands: read, clear.")]
    [InlineData(UcliCommandNames.Daemon, "Subcommand is required for command 'logs daemon'. Supported subcommands: read.")]
    public async Task LogsSourceWithoutAction_ReturnsJsonEnvelopeError (
        string source,
        string expectedMessage)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            source);

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
        Assert.Equal(expectedMessage, outputJson.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.UnitySubcommand, "Subcommand '--tail' is not recognized for command 'logs unity'.")]
    [InlineData(UcliCommandNames.Daemon, "Subcommand '--tail' is not recognized for command 'logs daemon'.")]
    public async Task LogsSourceWithReadOptionBeforeAction_ReturnsJsonEnvelopeError (
        string source,
        string expectedMessage)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            source,
            "--tail",
            "1");

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
        Assert.Equal(expectedMessage, outputJson.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LogsDaemonClear_ReturnsUnsupportedActionError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            UcliCommandNames.Daemon,
            UcliCommandNames.ClearSubcommand);

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
        Assert.Equal(
            "Subcommand 'clear' is not recognized for command 'logs daemon'.",
            outputJson.RootElement.GetProperty("message").GetString());
    }
}
