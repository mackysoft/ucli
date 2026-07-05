using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.CallServiceTestSupport;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class CallServicePlanFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPrePlanFails_DoesNotExecuteCallAndPreservesPlanPayload ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusError,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors:
                    [
                        new IpcError(PlanTokenErrorCodes.PlanTokenInvalid, "Plan failed.", null),
                    ],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        CallServiceInvocationAssert.PlanOnlyDispatched(ipcRequestExecutor);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Plan);
        Assert.Single(result.Output.Plan!.OpResults);
        Assert.Empty(result.Output.OpResults);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallFailsAfterSuccessfulPrePlan_RetainsPlanPayload ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: "issued-plan-token")),
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusError,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors:
                    [
                        new IpcError(PlanTokenErrorCodes.StateChangedSincePlan, "State changed.", null),
                    ],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Plan);
        Assert.Equal("issued-plan-token", result.Output.Plan!.PlanToken);
        Assert.Single(result.Output.Plan.OpResults);
        Assert.Single(result.Output.OpResults);
    }
}
