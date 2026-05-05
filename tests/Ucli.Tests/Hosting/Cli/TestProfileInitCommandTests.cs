using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class TestProfileInitCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubTestProfileInitService((_, _) => ValueTask.FromResult(TestProfileInitExecutionResult.Success(
            new TestProfileInitExecutionOutput("/repo/test.profile.json"))));
        var command = new TestProfileInitCommand(service);
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.Execute(() => command.Init(
            outputPath: "/repo/test.profile",
            force: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<TestProfileInitCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/test.profile", input.OutputPath);
        Assert.True(input.Force);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_WhenServiceReturnsFailure_UsesCommandResultFactoryEnvelope ()
    {
        var service = new StubTestProfileInitService((_, _) => ValueTask.FromResult(
            TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument("profile path already exists."))));
        var command = new TestProfileInitCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Init(
            outputPath: "/repo/test.profile.json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestProfileInit,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    private sealed class StubTestProfileInitService : ITestProfileInitService
    {
        private readonly Func<TestProfileInitCommandInput, CancellationToken, ValueTask<TestProfileInitExecutionResult>> handler;

        public StubTestProfileInitService (Func<TestProfileInitCommandInput, CancellationToken, ValueTask<TestProfileInitExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public TestProfileInitCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<TestProfileInitExecutionResult> ExecuteAsync (
            TestProfileInitCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
