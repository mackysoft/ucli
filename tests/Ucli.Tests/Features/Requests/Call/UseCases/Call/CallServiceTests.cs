using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;
using static MackySoft.Ucli.Tests.Helpers.Cli.CommandOptionNormalizationTestHelper;

namespace MackySoft.Ucli.Tests;

public sealed class CallServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallSucceedsWithoutWithPlan_SendsSingleCallRequest ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
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
        var preflightService = new StubPhaseExecutionPreflightService(PhaseExecutionPreflightResult.Success(preparedRequest));
        var timeProvider = new ManualTimeProvider();
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            timeProvider,
            preflightService: preflightService);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: "/repo/request.json",
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1234"),
                PlanToken: "plan-token-1",
                WithPlan: false,
                AllowDangerous: false,
                FailFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Single(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
        Assert.Equal(1, ipcRequestExecutor.CallCount);

        Assert.True(IpcPayloadCodec.TryDeserialize(
            ipcRequestExecutor.Invocations[0].Payload,
            out IpcExecuteRequest? executeRequest,
            out var payloadError));
        Assert.Equal(IpcPayloadReadError.None, payloadError);
        Assert.NotNull(executeRequest);
        Assert.Equal(UcliCommandIds.Call, ipcRequestExecutor.Invocations[0].Command);
        Assert.Equal(UcliCommandIds.Call, executeRequest!.Command);
        Assert.Equal("plan-token-1", executeRequest.PlanToken);
        Assert.True(executeRequest.FailFast);
        Assert.True(preflightService.ReceivedFailFast);
        Assert.Equal(UnityExecutionMode.Oneshot, ipcRequestExecutor.Invocations[0].Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), ipcRequestExecutor.Invocations[0].Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenWithPlanEnabled_IssuesPlanThenCallAndTransfersIssuedToken ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
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
                CreateResponse(
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

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Plan);
        Assert.Equal("issued-plan-token", result.Output.Plan!.PlanToken);
        Assert.Equal(2, ipcRequestExecutor.CallCount);

        Assert.True(IpcPayloadCodec.TryDeserialize(
            ipcRequestExecutor.Invocations[0].Payload,
            out IpcExecuteRequest? planRequest,
            out var planPayloadError));
        Assert.Equal(IpcPayloadReadError.None, planPayloadError);
        Assert.NotNull(planRequest);
        Assert.Equal(UcliCommandIds.Plan, planRequest!.Command);
        Assert.Null(planRequest.PlanToken);

        Assert.True(IpcPayloadCodec.TryDeserialize(
            ipcRequestExecutor.Invocations[1].Payload,
            out IpcExecuteRequest? callRequest,
            out var callPayloadError));
        Assert.Equal(IpcPayloadReadError.None, callPayloadError);
        Assert.NotNull(callRequest);
        Assert.Equal(UcliCommandIds.Call, callRequest!.Command);
        Assert.Equal("issued-plan-token", callRequest.PlanToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenWithPlanEnabledAndUserPlanTokenSpecified_PrefersUserToken ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: "issued-plan-token")),
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: "user-plan-token",
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            ipcRequestExecutor.Invocations[1].Payload,
            out IpcExecuteRequest? callRequest,
            out var callPayloadError));
        Assert.Equal(IpcPayloadReadError.None, callPayloadError);
        Assert.Equal("user-plan-token", callRequest!.PlanToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDangerousOpExistsWithoutFlag_ReturnsInvalidArgumentWithoutCallingUnity ()
    {
        var dangerousOperationName = "ucli.test.dangerous";
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(dangerousOperationName),
            request: CreateOpRequest(dangerousOperationName),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(dangerousOperationName, OperationPolicy.Dangerous)));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor();
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        Assert.Equal(0, ipcRequestExecutor.CallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationNotAllowed, error.Code);
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
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor();
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Equal(0, ipcRequestExecutor.CallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationNotAllowed, error.Code);
        Assert.Equal("edit-1", error.OpId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightHasValidationErrors_PreservesRequestIdPayload ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var preflightResult = PhaseExecutionPreflightResult.ValidationFailure(
            preparedRequest,
            [
                new ValidationError(
                    ValidationErrorCodes.OperationArgsInvalid,
                    "Operation args are invalid.",
                    "step-1"),
            ]);
        var service = CreateService(
            preflightResult,
            new SpyUnityIpcRequestExecutor());

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightInfrastructureErrorRetainsPreparedRequest_PreservesRequestIdPayload ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var preflightResult = PhaseExecutionPreflightResult.Failure(
            MackySoft.Ucli.Shared.Foundation.ExecutionError.InternalError("Operation metadata could not be loaded."),
            preparedRequest);
        var service = CreateService(
            preflightResult,
            new SpyUnityIpcRequestExecutor());

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightInfrastructureErrorHasCustomErrorCode_PreservesOriginalErrorCode ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var preflightResult = PhaseExecutionPreflightResult.Failure(
            ExecutionError.InternalError("Daemon is not running for mode=daemon."),
            preparedRequest,
            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
        var service = CreateService(
            preflightResult,
            new SpyUnityIpcRequestExecutor());

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, error.Code);
        Assert.Equal("Daemon is not running for mode=daemon.", error.Message);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPrePlanFails_DoesNotExecuteCallAndPreservesPlanPayload ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
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
                        new IpcError(IpcErrorCodes.PlanTokenInvalid, "Plan failed.", null),
                    ],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, ipcRequestExecutor.CallCount);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Plan);
        Assert.Single(result.Output.Plan!.OpResults);
        Assert.Empty(result.Output.OpResults);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallFailsAfterSuccessfulPrePlan_RetainsPlanPayload ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
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
                CreateResponse(
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
                        new IpcError(IpcErrorCodes.StateChangedSincePlan, "State changed.", null),
                    ],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode(null),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Plan);
        Assert.Equal("issued-plan-token", result.Output.Plan!.PlanToken);
        Assert.Single(result.Output.Plan.OpResults);
        Assert.Single(result.Output.OpResults);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPrePlanConsumesTimeoutBudget_PassesRemainingTimeoutToCall ()
    {
        var timeProvider = new ManualTimeProvider();
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: "issued-plan-token")),
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: null)))
        {
            TimeProvider = timeProvider,
            OnExecute = static context =>
            {
                if (context.Index == 1)
                {
                    context.TimeProvider!.Advance(TimeSpan.FromMilliseconds(200));
                }
            },
        };
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            timeProvider);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: true,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), ipcRequestExecutor.Invocations[0].Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), ipcRequestExecutor.Invocations[1].Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightConsumesTimeoutBudget_PassesRemainingTimeoutToUnityExecution ()
    {
        var timeProvider = new ManualTimeProvider();
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)));
        var preflightService = new StubPhaseExecutionPreflightService(PhaseExecutionPreflightResult.Success(preparedRequest))
        {
            TimeProvider = timeProvider,
            OnPrepare = static context =>
            {
                context.TimeProvider!.Advance(TimeSpan.FromMilliseconds(200));
            },
        };
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults: [],
                    errors: [],
                    planToken: null)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            timeProvider,
            preflightService: preflightService);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(ipcRequestExecutor.Invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), ipcRequestExecutor.Invocations[0].Timeout);
        Assert.False(preflightService.ReceivedFailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallResponseIncludesReadPostcondition_PersistsAndExposesIt ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, OperationPolicy.Advanced)));
        var readPostconditionStore = new TestMutationReadPostconditionStore();
        var readPostcondition = CreateReadPostcondition();
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: true,
                            Touched:
                            [
                                new IpcExecuteTouchedResource(
                                    Kind: IpcExecuteTouchedResourceKindNames.Scene,
                                    Path: "Assets/Scenes/Main.unity",
                                    Guid: null),
                            ]),
                    ],
                    errors: [],
                    planToken: null,
                    readPostcondition: readPostcondition)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            mutationReadPostconditionStore: readPostconditionStore);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.ReadPostcondition);
        Assert.Equal(1, readPostconditionStore.WriteCallCount);
        Assert.Equal("/repo", readPostconditionStore.LastStorageRoot);
        Assert.Equal("project-fingerprint", readPostconditionStore.LastProjectFingerprint);
        var requirement = Assert.Single(result.Output.ReadPostcondition!.Requirements);
        Assert.Equal(IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite, requirement.Surface);
        Assert.Equal("Assets/Scenes/Main.unity", requirement.ScenePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadPostconditionPersistenceFails_ReturnsToolErrorAndPreservesOutput ()
    {
        var preparedRequest = CreatePreparedRequest(
            requestJson: CreateOpRequestJson(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave),
            request: CreateOpRequest(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave),
            operationsByName: CreateOperationsByName(
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, OperationPolicy.Advanced)));
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            WriteResult = MutationReadPostconditionStoreOperationResult.Failure(
                ExecutionError.InternalError("Failed to persist mutation read postcondition.")),
        };
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "step-1",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: true,
                            Touched:
                            [
                                new IpcExecuteTouchedResource(
                                    Kind: IpcExecuteTouchedResourceKindNames.Scene,
                                    Path: "Assets/Scenes/Main.unity",
                                    Guid: null),
                            ]),
                    ],
                    errors: [],
                    planToken: null,
                    readPostcondition: CreateReadPostcondition())));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            mutationReadPostconditionStore: readPostconditionStore);

        var result = await service.Execute(
            new CallCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.OpResults);
        Assert.NotNull(result.Output.ReadPostcondition);
        Assert.Equal(1, readPostconditionStore.WriteCallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Equal("Failed to persist mutation read postcondition.", error.Message);
    }

    private static PhaseExecutionPreparedRequest CreatePreparedRequest (
        string requestJson,
        ValidateRequest request,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName)
    {
        return new PhaseExecutionPreparedRequest(
            PreparedRequest: new PreparedRequestContext(
                RequestJson: requestJson,
                InputSource: MackySoft.Ucli.Hosting.Cli.Requests.Input.RequestInputSource.StandardInput,
                Request: request,
                ProjectContext: new ProjectContext(
                    new ResolvedUnityProjectContext(
                        UnityProjectRoot: "/repo/UnityProject",
                        RepositoryRoot: "/repo",
                        ProjectFingerprint: "project-fingerprint",
                        PathSource: UnityProjectPathSource.CommandOption),
                    UcliConfig.CreateDefault(),
                    ConfigSource.Default)),
            OperationsByName: operationsByName);
    }

    private static CallService CreateService (
        PhaseExecutionPreflightResult preflightResult,
        SpyUnityIpcRequestExecutor ipcRequestExecutor,
        TimeProvider? timeProvider = null,
        RequestPreparationResult? requestPreparationResult = null,
        StubPhaseExecutionPreflightService? preflightService = null,
        TestMutationReadPostconditionStore? mutationReadPostconditionStore = null)
    {
        ArgumentNullException.ThrowIfNull(preflightResult);
        ArgumentNullException.ThrowIfNull(ipcRequestExecutor);

        requestPreparationResult ??= preflightResult.PreparedRequest != null
            ? RequestPreparationResult.Success(preflightResult.PreparedRequest.PreparedRequest)
            : throw new InvalidOperationException("A prepared request is required when request preparation is not explicitly configured.");

        return new CallService(
            new StubRequestPreparationService(requestPreparationResult),
            preflightService ?? new StubPhaseExecutionPreflightService(preflightResult),
            new CallDangerousOperationGuard(),
            new CallUnityExecutionService(ipcRequestExecutor, mutationReadPostconditionStore ?? new TestMutationReadPostconditionStore()),
            timeProvider);
    }

    private static IReadOnlyDictionary<string, UcliOperationDescriptor> CreateOperationsByName (params UcliOperationDescriptor[] operations)
    {
        var operationsByName = new Dictionary<string, UcliOperationDescriptor>(operations.Length, StringComparer.Ordinal);
        for (var i = 0; i < operations.Length; i++)
        {
            operationsByName[operations[i].Name] = operations[i];
        }

        return operationsByName;
    }

    private static UcliOperationDescriptor CreateOperationDescriptor (
        string name,
        OperationPolicy policy)
    {
        return new UcliOperationDescriptor(
            Name: name,
            Kind: UcliOperationKind.Mutation,
            Policy: policy,
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}""");
    }

    private static ValidateRequest CreateOpRequest (string operationName)
    {
        return new ValidateRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            Steps:
            [
                new ValidateRequestStep(
                    Kind: IpcRequestStepKind.Op,
                    StepId: "step-1",
                    Op: operationName,
                    Element: JsonSerializer.SerializeToElement(new
                    {
                        kind = "op",
                        id = "step-1",
                        op = operationName,
                        args = new { },
                    })),
            ]);
    }

    private static string CreateOpRequestJson (string operationName)
    {
        return JsonSerializer.Serialize(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            requestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            steps = new[]
            {
                new
                {
                    kind = "op",
                    id = "step-1",
                    op = operationName,
                    args = new { },
                },
            },
        });
    }

    private static ValidateRequest CreateEditRequest ()
    {
        using var document = JsonDocument.Parse(CreateEditRequestJson());

        return new ValidateRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            Steps:
            [
                new ValidateRequestStep(
                    Kind: IpcRequestStepKind.Edit,
                    StepId: "edit-1",
                    Op: null,
                    Element: document.RootElement.GetProperty("steps")[0].Clone()),
            ]);
    }

    private static string CreateEditRequestJson ()
    {
        return """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "edit",
                  "id": "edit-1",
                  "on": {
                    "scene": "Assets/Scenes/Main.unity"
                  },
                  "select": {
                    "gameObject": "Root/Spawner",
                    "cardinality": "one"
                  },
                  "actions": [
                    {
                      "kind": "ensureComponent",
                      "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                      "as": "collider"
                    },
                    {
                      "kind": "set",
                      "target": "$collider",
                      "values": {
                        "isTrigger": true
                      }
                    }
                  ],
                  "commit": "context"
                }
              ]
            }
            """;
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken,
        IpcExecuteReadPostcondition? readPostcondition = null)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-1",
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(opResults)
            {
                PlanToken = planToken,
                ReadPostcondition = readPostcondition,
            }),
            Errors: errors);
    }

    private static IpcExecuteReadPostcondition CreateReadPostcondition ()
    {
        return new IpcExecuteReadPostcondition(
        [
            new IpcExecuteReadPostconditionRequirement(
                Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T01:02:03+00:00"))
            {
                ScenePath = "Assets/Scenes/Main.unity",
            },
        ]);
    }

    private sealed class StubRequestPreparationService : IRequestPreparationService
    {
        private readonly RequestPreparationResult result;

        public StubRequestPreparationService (RequestPreparationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<ParsedRequestResult> ReadAndParse (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public ValueTask<RequestPreparationResult> Prepare (
            string? requestPath,
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubPhaseExecutionPreflightService : IPhaseExecutionPreflightService
    {
        private readonly PhaseExecutionPreflightResult result;

        public StubPhaseExecutionPreflightService (PhaseExecutionPreflightResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ManualTimeProvider? TimeProvider { get; init; }

        public Action<PreflightInvocationContext>? OnPrepare { get; init; }

        public bool ReceivedFailFast { get; private set; }

        public ValueTask<PhaseExecutionPreflightResult> Prepare (
            PreparedRequestContext preparedRequest,
            UnityExecutionMode mode,
            ExecutionDeadline deadline,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(preparedRequest);
            ReceivedFailFast = failFast;
            OnPrepare?.Invoke(new PreflightInvocationContext(deadline, TimeProvider));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyUnityIpcRequestExecutor : IUnityRequestExecutor
    {
        private readonly Queue<UnityRequestExecutionResult> results = new();

        public SpyUnityIpcRequestExecutor (params UnityRequestExecutionResult[] results)
        {
            foreach (var result in results)
            {
                this.results.Enqueue(result);
            }
        }

        public int CallCount => Invocations.Count;

        public List<Invocation> Invocations { get; } = [];

        public ManualTimeProvider? TimeProvider { get; init; }

        public Action<InvocationContext>? OnExecute { get; init; }

        public ValueTask<UnityRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            string method,
            JsonElement payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var invocation = new Invocation(command, mode, timeout, config, unityProject, method, payload.Clone(), cancellationToken);
            Invocations.Add(invocation);
            OnExecute?.Invoke(new InvocationContext(Invocations.Count, invocation, TimeProvider));

            if (results.Count == 0)
            {
                throw new InvalidOperationException("No queued Unity IPC result remains.");
            }

            return ValueTask.FromResult(results.Dequeue());
        }
    }

    private sealed record Invocation (
        UcliCommand Command,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        UcliConfig Config,
        ResolvedUnityProjectContext UnityProject,
        string Method,
        JsonElement Payload,
        CancellationToken CancellationToken);

    private sealed record InvocationContext (
        int Index,
        Invocation Invocation,
        ManualTimeProvider? TimeProvider);

    private sealed record PreflightInvocationContext (
        ExecutionDeadline Deadline,
        ManualTimeProvider? TimeProvider);
}