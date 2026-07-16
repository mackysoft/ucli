using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static MackySoft.Ucli.Application.Tests.PlanServiceTestSupport;

public sealed class PlanServiceUnityResponseTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityResponseOmitsPlanToken_ReturnsInternalErrorWithPartialOpResults ()
    {
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess(
            planToken: null,
            opResults:
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("step-1"),
                    Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Phase: IpcExecuteOperationPhase.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: []),
            ]));
        var service = CreateService(unityRequestExecutor: unityIpcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.OpResults);
        Assert.Null(result.Output.PlanToken);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("planToken", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(UnityExecutionToolErrorCodeCases))]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityExecutionFailsWithToolErrorCode_ReturnsToolErrorAndPreservesPayload (UcliCode errorCode)
    {
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResultTestFactory.Failure(
                "Unity execution failed.",
                errorCode)));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.NotNull(result.Output.ReadIndex);
        var error = Assert.Single(result.Errors);
        Assert.Equal(errorCode, error.Code);
    }

    public static TheoryData<UcliCode> UnityExecutionToolErrorCodeCases ()
    {
        var data = new TheoryData<UcliCode>();
        foreach (var errorCode in UnityExecutionToolErrorCodes)
        {
            data.Add(errorCode);
        }

        return data;
    }
}
