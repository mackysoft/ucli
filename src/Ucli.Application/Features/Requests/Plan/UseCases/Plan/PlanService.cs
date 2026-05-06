using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;

/// <summary> Implements the <c>plan</c> workflow by combining static-validation preflight and Unity IPC plan execution. </summary>
internal sealed class PlanService : IPlanService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidationPreflightService requestStaticValidationPreflightService;

    private readonly IUnityRequestExecutor unityIpcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlanService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidationPreflightService"> The shared static-validation preflight dependency. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlanService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidationPreflightService requestStaticValidationPreflightService,
        IUnityRequestExecutor unityIpcRequestExecutor)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidationPreflightService = requestStaticValidationPreflightService ?? throw new ArgumentNullException(nameof(requestStaticValidationPreflightService));
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<PlanServiceResult> Execute (
        PlanCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var requestPreparationResult = await requestPreparationService.Prepare(
                input.ProjectPath,
                input.RequestJson,
                cancellationToken)
            .ConfigureAwait(false);
        if (!requestPreparationResult.IsSuccess)
        {
            return PlanFailureResultFactory.FromExecutionError(requestPreparationResult.Error!);
        }

        var requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.Prepare(
                requestPreparationResult.PreparedRequest!,
                input.ReadIndexMode,
                cancellationToken)
            .ConfigureAwait(false);

        var preparedRequest = requestStaticValidationPreflightResult.PreparedRequest;
        var baseOutput = PlanExecutionOutputFactory.TryCreateBase(preparedRequest, requestStaticValidationPreflightResult.ReadIndex);
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

        var executionResult = await unityIpcRequestExecutor.Execute(
                UcliCommandIds.Plan,
                executionMode,
                timeoutResolutionResult.Timeout!.Value,
                preparedRequest.ProjectContext.Config,
                preparedRequest.ProjectContext.UnityProject,
                CreateExecuteRequestPayload(preparedRequest.RequestJson, input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var error = RequestServiceResultPolicy.FromTransportFailure(
                executionResult.ErrorCode,
                executionResult.Message);
            return PlanServiceResult.Failure(
                executionResult.Message,
                [
                    error,
                ],
                RequestServiceResultPolicy.ResolveOutcome(error.Code),
                baseOutput);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(executionResult.Response!);
        var executionOutput = baseOutput with
        {
            OpResults = convertedResponse.OpResults,
        };
        if (!convertedResponse.IsSuccess)
        {
            return PlanServiceResult.Failure(
                RequestServiceResultPolicy.ResolveFailureMessage(convertedResponse.Errors, "uCLI plan failed."),
                convertedResponse.Errors,
                convertedResponse.Outcome,
                executionOutput);
        }

        if (string.IsNullOrWhiteSpace(convertedResponse.PlanToken))
        {
            return PlanFailureResultFactory.FromExecutionError(
                ExecutionError.InternalError("Execute response payload is invalid. The 'planToken' field is missing."),
                executionOutput,
                IpcErrorCodes.InternalError);
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
        bool failFast)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        using var document = JsonDocument.Parse(requestJson);
        return new UnityRequestPayload.ExecuteJson(
            UcliCommandIds.Plan,
            document.RootElement.Clone(),
            failFast);
    }

}
