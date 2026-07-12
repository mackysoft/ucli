using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.CallServiceTestSupport;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class CallServiceWorkflowTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallSucceedsWithoutWithPlan_SendsSingleCallRequest ()
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
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: null)));
        var preflightService = new RecordingPhaseExecutionPreflightService
        {
            Result = PhaseExecutionPreflightResult.Success(preparedRequest),
        };
        var timeProvider = new ManualTimeProvider();
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            timeProvider,
            preflightService: preflightService);

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1234"),
                PlanToken: "plan-token-1",
                WithPlan: false,
                AllowDangerous: false,
                FailFast: true,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.Single(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
        CallServiceInvocationAssert.SingleCallDispatched(
            ipcRequestExecutor,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedPlanToken: "plan-token-1",
            expectedFailFast: true,
            expectedAllowDangerous: false,
            expectedAllowPlayMode: false);
        PhaseExecutionPreflightInvocationAssert.PreparedOnce(
            preflightService,
            preparedRequest,
            UnityExecutionMode.Oneshot,
            expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenWithPlanEnabled_IssuesPlanThenCallAndTransfersIssuedToken ()
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
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}""")
            {
                AllowPlayMode = true,
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Plan);
        Assert.Equal("issued-plan-token", result.Output.Plan!.PlanToken);
        var executePair = CallServiceInvocationAssert.PlanThenCallDispatched(
            ipcRequestExecutor,
            expectedCallPlanToken: "issued-plan-token",
            expectedAllowDangerous: false,
            expectedAllowPlayMode: true);

        Assert.Equal(RequestId, result.Output.RequestId);
        Assert.False(executePair.PlanRequest.ExecuteArguments.TryGetProperty("requestId", out _));
        Assert.False(executePair.CallRequest.ExecuteArguments.TryGetProperty("requestId", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExecutionOwnerCommandIsProvided_UsesOwnerForWorkflowAndKeepsExecutePhaseCommands ()
    {
        var config = UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                ["call"] = 1111,
                ["eval"] = 7777,
            },
        };
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe,
            config);
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
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: null)));
        var timeProvider = new ManualTimeProvider();
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            timeProvider);

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: null,
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}""")
            {
                ExecutionOwnerCommand = UcliCommandIds.Eval,
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("uCLI eval completed.", result.Message);
        CallServiceInvocationAssert.PlanThenCallDispatchedByOwner(
            ipcRequestExecutor,
            UcliCommandIds.Eval,
            expectedPlanTimeout: TimeSpan.FromMilliseconds(7777),
            expectedCallTimeout: TimeSpan.FromMilliseconds(7777));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenWithPlanEnabledAndUserPlanTokenSpecified_PrefersUserToken ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: "issued-plan-token")),
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: "user-plan-token",
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var executePair = CallServiceInvocationAssert.PlanThenCallDispatched(
            ipcRequestExecutor,
            expectedCallPlanToken: "user-plan-token",
            expectedAllowDangerous: false,
            expectedAllowPlayMode: false);
        Assert.Equal("user-plan-token", executePair.CallRequest.PlanToken);
    }

}
