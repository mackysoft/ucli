using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingTestRunService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RunAsync(
            executionMode: "unsupported",
            cancellationToken: CancellationToken.None));

        TestRunCommandAssert.InvalidInputReturnedWithoutExecution(result, service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenTestPlatformIsWhitespace_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingTestRunService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RunAsync(
            testPlatform: " ",
            cancellationToken: CancellationToken.None));

        TestRunCommandAssert.InvalidInputReturnedWithoutExecution(result, service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithUnsupportedFormat_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingTestRunService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "yaml",
            cancellationToken: CancellationToken.None));

        TestRunCommandAssert.InvalidArgumentReturnedWithoutExecutionAndStandardError(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenCancellationIsRequested_WritesTestRunCommandResult ()
    {
        var service = new RecordingTestRunService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            cancellationToken: cancellationTokenSource.Token));

        TestRunCommandAssert.CanceledBeforeExecution(result, service);
    }
}
