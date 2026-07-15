using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class LogsCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedLogsServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Logs_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Logs);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Logs);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LogsReadCommands_WithInvalidOptions_ReturnJsonEnvelopeError ()
    {
        foreach (LogsInvalidOptionCase testCase in GetLogsInvalidOptionCases())
        {
            var result = await testCase.RunAsync();

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            CommandResultAssert.HasInvalidArgumentEnvelope(
                outputJson.RootElement,
                testCase.ExpectedCommand);
            CommandResultAssert.HasSingleError(
                outputJson.RootElement,
                expectedCode: "INVALID_ARGUMENT");
            Assert.Contains(
                testCase.ExpectedMessageFragment,
                outputJson.RootElement.GetProperty("message").GetString(),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LogsUnityClear_WithReadOption_ReturnsJsonEnvelopeErrorForClearCommand ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            UcliCommandNames.UnitySubcommand,
            UcliCommandNames.ClearSubcommand,
            "--tail",
            "1");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.LogsUnityClear);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    private static Task<CommandExecutionResult> RunLogsDaemonReadCommandAsync (
        string? queryTarget = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<LogsDaemonReadCommand>(
                    SharedLogsServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .ReadAsync(
                    queryTarget: queryTarget,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
    }

    private static Task<CommandExecutionResult> RunLogsUnityReadCommandAsync (
        int? tail = null,
        string? stackTrace = null,
        bool stream = false,
        int? pollIntervalMilliseconds = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<LogsUnityReadCommand>(
                    SharedLogsServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .ReadAsync(
                    tail: tail,
                    stackTrace: stackTrace,
                    stream: stream,
                    pollIntervalMilliseconds: pollIntervalMilliseconds,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
    }

    private static LogsInvalidOptionCase[] GetLogsInvalidOptionCases ()
    {
        return
        [
            new(
                UcliCommandNames.LogsDaemonRead,
                () => RunLogsDaemonReadCommandAsync(queryTarget: "stack"),
                "queryTarget 'stack' is not supported for daemon logs."),
            new(
                UcliCommandNames.LogsDaemonRead,
                () => RunLogsDaemonReadCommandAsync(timeout: "0"),
                "timeout must be a positive integer milliseconds value. Actual: 0."),
            new(
                UcliCommandNames.LogsUnityRead,
                () => RunLogsUnityReadCommandAsync(tail: 1, stackTrace: "unsupported"),
                "stackTrace must be one of: none, error, all. Actual: unsupported."),
            new(
                UcliCommandNames.LogsUnityRead,
                () => RunLogsUnityReadCommandAsync(timeout: "0"),
                "timeout must be a positive integer milliseconds value. Actual: 0."),
            new(
                UcliCommandNames.LogsUnityRead,
                () => RunLogsUnityReadCommandAsync(stream: true, pollIntervalMilliseconds: 49),
                "pollIntervalMilliseconds must be between 50 and 60000. Actual: 49."),
        ];
    }

    private sealed record LogsInvalidOptionCase (
        string ExpectedCommand,
        Func<Task<CommandExecutionResult>> RunAsync,
        string ExpectedMessageFragment);
}
