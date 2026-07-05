using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.CallServiceTestSupport;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class CallServiceDangerousOperationGuardTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenWithPlanAndDangerousFlagEnabled_PassesAllowDangerousToPlanAndCall ()
    {
        var dangerousOperationName = "ucli.test.dangerous";
        var preparedRequest = CreateSingleOperationPreparedRequest(dangerousOperationName, OperationPolicy.Dangerous);
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: dangerousOperationName,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: "issued-plan-token")),
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: dangerousOperationName,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: true,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: true,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        CallServiceInvocationAssert.PlanThenCallDispatched(
            ipcRequestExecutor,
            expectedCallPlanToken: "issued-plan-token",
            expectedAllowDangerous: true,
            expectedAllowPlayMode: false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDangerousOpExistsWithFlag_PassesAllowDangerousToUnity ()
    {
        var dangerousOperationName = "ucli.test.dangerous";
        var preparedRequest = CreateSingleOperationPreparedRequest(dangerousOperationName, OperationPolicy.Dangerous);
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: dangerousOperationName,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: true,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: true,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var execution = CallServiceInvocationAssert.SingleCallDispatched(ipcRequestExecutor);
        Assert.True(execution.Request.AllowDangerous);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDangerousOpExistsWithoutFlag_ReturnsInvalidArgumentWithoutCallingUnity ()
    {
        var dangerousOperationName = "ucli.test.dangerous";
        var preparedRequest = CreateSingleOperationPreparedRequest(dangerousOperationName, OperationPolicy.Dangerous);
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, error.Code);
        Assert.Equal("step-1", error.OpId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDangerousEditLoweringExistsWithoutFlag_ReturnsInvalidArgumentWithoutCallingUnity ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateEditRequestJson(),
            request: CreateEditRequest(),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompEnsure, OperationPolicy.Dangerous),
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSet, OperationPolicy.Safe)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, error.Code);
        Assert.Equal("edit-1", error.OpId);
    }
}
