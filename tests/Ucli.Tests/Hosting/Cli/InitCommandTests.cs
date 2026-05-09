using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Features.Init.UseCases.Init;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Init;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class InitCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubInitService((_, _) => ValueTask.FromResult(InitExecutionResult.Success(
            new InitExecutionOutput(
                ConfigPath: "/repo/.ucli/config.json",
                GitIgnorePath: "/repo/.ucli/.gitignore"))));
        var command = new InitCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.InitAsync(
            force: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<InitCommandInput>(service.CapturedInput);
        Assert.True(input.Force);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_WhenServiceReturnsFailure_UsesCommandResultFactoryEnvelope ()
    {
        var service = new StubInitService((_, _) => ValueTask.FromResult(
            InitExecutionResult.Failure(ExecutionError.InvalidArgument("template files already exist."))));
        var command = new InitCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.InitAsync(
            force: false,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Init,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    private sealed class StubInitService : IInitService
    {
        private readonly Func<InitCommandInput, CancellationToken, ValueTask<InitExecutionResult>> handler;

        public StubInitService (Func<InitCommandInput, CancellationToken, ValueTask<InitExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public InitCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<InitExecutionResult> ExecuteAsync (
            InitCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
