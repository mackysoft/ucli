using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Call;
using MackySoft.Ucli.Hosting.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_UsesCallServiceAndWritesCommandResult ()
    {
        var service = new StubCallService((input, _) => ValueTask.FromResult(CallServiceResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults:
                [
                    new IpcExecuteOperationResult(
                        OpId: "step-1",
                        Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhaseNames.Call,
                        Applied: true,
                        Changed: false,
                        Touched: []),
                ],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    OpResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    PlanToken: "plan-token-1")),
            "uCLI call completed.")));
        var command = new CallCommand(service);
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Call(
            requestPath: "/repo/request.json",
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            planToken: "user-token",
            withPlan: true,
            allowDangerous: true,
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/request.json", service.CapturedInput!.RequestPath);
        Assert.Equal("/repo/UnityProject", service.CapturedInput.ProjectPath);
        Assert.Equal("oneshot", service.CapturedInput.Mode);
        Assert.Equal("1234", service.CapturedInput.Timeout);
        Assert.Equal("user-token", service.CapturedInput.PlanToken);
        Assert.True(service.CapturedInput.WithPlan);
        Assert.True(service.CapturedInput.AllowDangerous);
        Assert.True(service.CapturedInput.FailFast);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI call completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 1)
                .HasProperty("plan", plan => plan
                    .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                    .HasArrayLength("opResults", 1)
                    .HasString("planToken", "plan-token-1")));
    }

    private sealed class StubCallService : ICallService
    {
        private readonly Func<CallCommandInput, CancellationToken, ValueTask<CallServiceResult>> handler;

        public StubCallService (Func<CallCommandInput, CancellationToken, ValueTask<CallServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public CallCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<CallServiceResult> Execute (
            CallCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}