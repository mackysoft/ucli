using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static MackySoft.Ucli.Application.Tests.PlanServiceTestSupport;

public sealed class PlanServiceAllowPlayModeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeIsSpecified_SkipsReadIndexPreflightAndUsesLiveStaticValidation ()
    {
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess("plan-token-1"));
        var staticPreflightService = CreateSuccessfulPreflightService();
        var staticValidationService = new RecordingRequestStaticValidationService
        {
            Result = ValidationResult.Success(),
        };
        var service = CreateService(
            staticPreflightService: staticPreflightService,
            staticValidationService: staticValidationService,
            unityRequestExecutor: unityIpcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                failFast: true,
                allowPlayMode: true),
            CancellationToken.None);

        PlanServiceInvocationAssert.AllowPlayModeUsedLiveStaticValidation(
            result,
            staticPreflightService,
            staticValidationService);
        var execution = PlanServiceInvocationAssert.PlanDispatched(unityIpcRequestExecutor);
        Assert.Equal(UnityExecutionMode.Oneshot, execution.Invocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), execution.Invocation.Timeout);
        Assert.True(execution.Request.FailFast);
        Assert.True(execution.Request.AllowPlayMode);
        Assert.Null(execution.Request.PlanToken);
        Assert.False(execution.Request.ExecuteArguments.TryGetProperty("requestId", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeAndReadIndexModeAreSpecified_ReturnsInvalidArgumentWithoutPreflight ()
    {
        var staticPreflightService = CreateSuccessfulPreflightService();
        var staticValidationService = new RecordingRequestStaticValidationService
        {
            Result = ValidationResult.Success(),
        };
        var service = CreateService(
            staticPreflightService: staticPreflightService,
            staticValidationService: staticValidationService,
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                readIndexMode: ReadIndexMode.Disabled,
                failFast: true,
                allowPlayMode: true),
            CancellationToken.None);

        PlanServiceInvocationAssert.ReadIndexModeRejectedBeforeStaticValidation(
            result,
            staticPreflightService,
            staticValidationService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeStaticValidationFails_ReturnsFailureWithoutCallingPlan ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                OperationAuthorizationErrorCodes.OperationNotAllowed,
                "Edit step 'step-1' requires operation 'ucli.comp.set'. Operation is blocked.",
                new IpcExecuteStepId("step-1")),
        ];
        var service = CreateService(
            staticPreflightService: CreateSuccessfulPreflightService(),
            staticValidationService: new RecordingRequestStaticValidationService
            {
                Result = ValidationResult.Invalid(validationErrors),
            },
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(allowPlayMode: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        var error = Assert.Single(result.Errors);
        Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, error.Code);
    }
}
