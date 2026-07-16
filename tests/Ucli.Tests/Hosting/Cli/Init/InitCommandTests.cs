using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Init;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class InitCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingInitService((_, _) => ValueTask.FromResult(InitExecutionResult.Success(
            new InitExecutionOutput(
                ConfigPath: "/repo/.ucli/config.json",
                GitIgnorePath: "/repo/.ucli/.gitignore"))));
        var command = new InitCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.InitAsync(
            force: true,
            cancellationToken: cancellationTokenSource.Token));

        InitCommandAssert.SucceededWithForceInput(
            result,
            service,
            cancellationTokenSource.Token,
            expectedForce: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_WhenServiceReturnsFailure_UsesCommandResultFactoryEnvelope ()
    {
        var service = new RecordingInitService((_, _) => ValueTask.FromResult(
            InitExecutionResult.Failure(ExecutionError.InvalidArgument("template files already exist."))));
        var command = new InitCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.InitAsync(
            force: false,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.Init);
    }

}
