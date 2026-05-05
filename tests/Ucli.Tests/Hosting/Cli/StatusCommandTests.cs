using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Status.Common.Contracts;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Status;

namespace MackySoft.Ucli.Tests;

public sealed class StatusCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubStatusService((_, _) => ValueTask.FromResult(StatusExecutionResult.Success(
            new StatusExecutionOutput(
                DaemonStatus: "notRunning",
                UnityVersion: "6000.1.4f1",
                ServerVersion: null,
                LifecycleState: null,
                BlockingReason: null,
                CompileState: null,
                CompileGeneration: null,
                DomainReloadGeneration: null,
                CanAcceptExecutionRequests: false,
                Runtime: null,
                TimeoutMilliseconds: 1234))));
        var command = new StatusCommand(service);
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.Execute(() => command.Status(
            projectPath: "/repo/UnityProject",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<StatusCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(1234, input.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubStatusService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new StatusCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Status(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Status,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    private sealed class StubStatusService : IStatusService
    {
        private readonly Func<StatusCommandInput, CancellationToken, ValueTask<StatusExecutionResult>> handler;

        public StubStatusService (Func<StatusCommandInput, CancellationToken, ValueTask<StatusExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public StatusCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<StatusExecutionResult> Execute (
            StatusCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
