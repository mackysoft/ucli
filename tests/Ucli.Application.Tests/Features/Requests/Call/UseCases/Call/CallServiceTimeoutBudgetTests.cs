using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.CallServiceTestSupport;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class CallServiceTimeoutBudgetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPrePlanConsumesTimeoutBudget_PassesRemainingTimeoutToCall ()
    {
        var timeProvider = new ManualTimeProvider();
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
                    planToken: null)))
        {
            OnExecute = context =>
            {
                if (context.Index == 1)
                {
                    timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                }
            },
        };
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            timeProvider);

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        CallServiceInvocationAssert.PlanThenCallDispatchedWithTimeouts(
            ipcRequestExecutor,
            expectedPlanTimeout: TimeSpan.FromMilliseconds(1200),
            expectedCallTimeout: TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightConsumesTimeoutBudget_PassesRemainingTimeoutToUnityExecution ()
    {
        var timeProvider = new ManualTimeProvider();
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var preflightService = new RecordingPhaseExecutionPreflightService
        {
            Result = PhaseExecutionPreflightResult.Success(preparedRequest),
            OnPrepare = _ =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            },
        };
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: null)));
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
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        CallServiceInvocationAssert.SingleCallDispatched(
            ipcRequestExecutor,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1000),
            expectedPlanToken: null,
            expectedFailFast: false,
            expectedAllowDangerous: false,
            expectedAllowPlayMode: false);
        PhaseExecutionPreflightInvocationAssert.PreparedOnce(
            preflightService,
            preparedRequest,
            UnityExecutionMode.Oneshot,
            expectedFailFast: false);
    }
}
