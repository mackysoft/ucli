using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static MackySoft.Ucli.Application.Tests.PlanServiceTestSupport;

public sealed class PlanServiceDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStaticPreflightSucceeds_UsesPlanIpcPayloadAndReturnsSuccess ()
    {
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess(
            "plan-token-1",
            [
                new IpcExecuteOperationResult(
                    OpId: "step-1",
                    Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Phase: IpcExecuteOperationPhaseNames.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: []),
            ]));
        var service = CreateService(
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = CreateSuccessPreflightResult(
                    CreateReadIndexInfo(
                        used: true,
                        hit: true,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: null)),
            },
            unityRequestExecutor: unityIpcRequestExecutor);

        var result = await service.ExecuteAsync(
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("uCLI plan completed.", result.Message);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Equal("plan-token-1", result.Output.PlanToken);
        Assert.True(result.Output.ReadIndex.Used);
        PlanServiceInvocationAssert.PlanDispatched(
            unityIpcRequestExecutor,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedFailFast: true,
            expectedAllowPlayMode: false,
            expectedRequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");
    }
}
