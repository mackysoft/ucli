using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;

/// <summary> Implements the <c>plan</c> workflow by combining static-validation preflight and Unity IPC plan execution. </summary>
internal sealed class PlanService : IPlanService
{
    private const string PlayModeReadIndexFallbackReason = "Play Mode mutation uses live Unity state.";

    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidationPreflightService requestStaticValidationPreflightService;

    private readonly IRequestStaticValidationService requestStaticValidationService;

    private readonly IUnityRequestExecutor unityIpcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlanService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidationPreflightService"> The shared static-validation preflight dependency. </param>
    /// <param name="requestStaticValidationService"> The live-catalog static-validation dependency used when readIndex must be bypassed. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlanService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidationPreflightService requestStaticValidationPreflightService,
        IRequestStaticValidationService requestStaticValidationService,
        IUnityRequestExecutor unityIpcRequestExecutor)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidationPreflightService = requestStaticValidationPreflightService ?? throw new ArgumentNullException(nameof(requestStaticValidationPreflightService));
        this.requestStaticValidationService = requestStaticValidationService ?? throw new ArgumentNullException(nameof(requestStaticValidationService));
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<PlanServiceResult> ExecuteAsync (
        PlanCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (input.AllowPlayMode && input.ReadIndexMode != null)
        {
            return PlanFailureResultFactory.FromExecutionError(
                ExecutionError.InvalidArgument("--allowPlayMode cannot be combined with --readIndexMode."));
        }

        var requestPreparationResult = await requestPreparationService.PrepareAsync(
                input.ProjectPath,
                input.RequestJson,
                cancellationToken)
            .ConfigureAwait(false);
        if (!requestPreparationResult.IsSuccess)
        {
            return PlanFailureResultFactory.FromExecutionError(requestPreparationResult.Error!);
        }

        var preparedRequest = requestPreparationResult.PreparedRequest!;
        var baseOutput = PlanExecutionOutputFactory.CreateBase(
            preparedRequest,
            ReadIndexInfoFactory.Unity(PlayModeReadIndexFallbackReason));
        if (!input.AllowPlayMode)
        {
            var requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.PrepareAsync(
                    preparedRequest,
                    input.ReadIndexMode,
                    cancellationToken)
                .ConfigureAwait(false);

            preparedRequest = requestStaticValidationPreflightResult.PreparedRequest;
            baseOutput = PlanExecutionOutputFactory.TryCreateBase(preparedRequest, requestStaticValidationPreflightResult.ReadIndex);
            if (requestStaticValidationPreflightResult.Error != null)
            {
                return PlanFailureResultFactory.FromExecutionError(
                    requestStaticValidationPreflightResult.Error,
                    baseOutput,
                    requestStaticValidationPreflightResult.ErrorCode);
            }

            if (requestStaticValidationPreflightResult.HasValidationErrors)
            {
                return PlanFailureResultFactory.FromValidationErrors(
                    requestStaticValidationPreflightResult.ValidationErrors,
                    baseOutput);
            }

            preparedRequest = requestStaticValidationPreflightResult.PreparedRequest!;
            baseOutput = PlanExecutionOutputFactory.CreateBase(preparedRequest, requestStaticValidationPreflightResult.ReadIndex!);
        }
        else
        {
            var validationResult = await requestStaticValidationService.ValidateAsync(
                    preparedRequest.Request,
                    preparedRequest.ProjectContext,
                    cancellationToken)
                .ConfigureAwait(false);
            if (validationResult.Error != null)
            {
                return PlanFailureResultFactory.FromExecutionError(
                    validationResult.Error,
                    baseOutput);
            }

            if (!validationResult.IsValid)
            {
                return PlanFailureResultFactory.FromValidationErrors(
                    validationResult.Errors,
                    baseOutput);
            }
        }

        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Plan,
            preparedRequest.ProjectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return PlanFailureResultFactory.FromExecutionError(
                timeoutResolutionResult.Error!,
                baseOutput);
        }

        var executionMode = input.Mode ?? UnityExecutionMode.Auto;

        var executionResult = await unityIpcRequestExecutor.ExecuteAsync(
                UcliCommandIds.Plan,
                executionMode,
                timeoutResolutionResult.Timeout!.Value,
                preparedRequest.ProjectContext.Config,
                preparedRequest.ProjectContext.UnityProject,
                CreateExecuteRequestPayload(preparedRequest.RequestJson, input.FailFast, input.AllowPlayMode),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(executionResult.FailureInfo!);
            return PlanServiceResult.Failure(
                failure.Message,
                [
                    failure,
                ],
                baseOutput);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(executionResult.Response!);
        var executionOutput = baseOutput with
        {
            Project = convertedResponse.Project ?? baseOutput.Project,
            OpResults = convertedResponse.OpResults,
            ContractViolations = convertedResponse.ContractViolations,
        };
        if (!convertedResponse.IsSuccess)
        {
            var failures = RequestFailureNormalizer.FromOperationErrors(convertedResponse.Errors, "uCLI plan failed.");
            return PlanServiceResult.Failure(
                RequestFailureNormalizer.ResolveMessage(failures, "uCLI plan failed."),
                failures,
                executionOutput);
        }

        if (string.IsNullOrWhiteSpace(convertedResponse.PlanToken))
        {
            return PlanFailureResultFactory.FromExecutionError(
                ExecutionError.InternalError("Execute response payload is invalid. The 'planToken' field is missing."),
                executionOutput,
                UcliCoreErrorCodes.InternalError);
        }

        return PlanServiceResult.Success(
            executionOutput with
            {
                PlanToken = convertedResponse.PlanToken,
            },
            "uCLI plan completed.");
    }

    private static UnityRequestPayload CreateExecuteRequestPayload (
        string requestJson,
        bool failFast,
        bool allowPlayMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        using var document = JsonDocument.Parse(requestJson);
        return new UnityRequestPayload.ExecuteJson(
            UcliCommandIds.Plan,
            document.RootElement.Clone(),
            failFast,
            AllowPlayMode: allowPlayMode);
    }

}
