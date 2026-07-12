using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.CallServiceTestSupport;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class CallServicePreflightFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightHasValidationErrors_PreservesRequestIdPayload ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
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
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
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
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightInfrastructureErrorRetainsPreparedRequest_PreservesRequestIdPayload ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var preflightResult = PhaseExecutionPreflightResult.Failure(
            ExecutionError.InternalError("Operation metadata could not be loaded."),
            preparedRequest);
        var service = CreateService(
            preflightResult,
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
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
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightInfrastructureErrorHasCustomErrorCode_PreservesOriginalErrorCode ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var preflightResult = PhaseExecutionPreflightResult.Failure(
            ExecutionError.InternalError("Daemon is not running for mode=daemon."),
            preparedRequest,
            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
        var service = CreateService(
            preflightResult,
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: NormalizeTimeout(null),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, error.Code);
        Assert.Equal("Daemon is not running for mode=daemon.", error.Message);
        Assert.NotNull(result.Output);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.Empty(result.Output.OpResults);
        Assert.Null(result.Output.Plan);
    }
}
