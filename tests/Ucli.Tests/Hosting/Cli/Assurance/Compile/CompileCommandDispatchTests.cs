using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CompileCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingCompileService((_, _, _) => ValueTask.FromResult(CompileExecutionResult.Success(CreateOutput())));
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.CompileAsync(
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        CompileCommandAssert.SucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 1234);
    }
}
