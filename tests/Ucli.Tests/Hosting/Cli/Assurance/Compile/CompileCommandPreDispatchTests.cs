using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingCompileService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteAsync(() => command.CompileAsync(
            mode: "unknown",
            cancellationToken: CancellationToken.None));

        CompileCommandAssert.InvalidArgumentReturnedWithoutCompileExecution(
            result,
            service,
            expectEmptyStandardError: false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WhenFormatIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingCompileService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            format: "yaml",
            cancellationToken: CancellationToken.None));

        CompileCommandAssert.InvalidArgumentReturnedWithoutCompileExecution(
            result,
            service,
            expectEmptyStandardError: true);
    }
}
