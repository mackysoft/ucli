using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Postprocessing;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Orchestrates Unity IPC plan and call passes for prepared <c>call</c> requests. </summary>
internal sealed class CallUnityExecutionService : ICallUnityExecutionService
{
    private readonly IUnityRequestExecutor unityIpcRequestExecutor;

    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;

    /// <summary> Initializes a new instance of the <see cref="CallUnityExecutionService" /> class. </summary>
    public CallUnityExecutionService (
        IUnityRequestExecutor unityIpcRequestExecutor,
        IMutationReadPostconditionStore mutationReadPostconditionStore)
    {
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
    }

    /// <inheritdoc />
    public async ValueTask<CallServiceResult> ExecuteAsync (
        PhaseExecutionPreparedRequest preparedRequest,
        UnityExecutionMode mode,
        CallCommandInput input,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(input);

        var baseOutput = CallExecutionOutputFactory.CreateBase(preparedRequest.Request.RequestId!);
        var effectivePlanToken = StringValueNormalizer.TrimToNull(input.PlanToken);

        if (input.WithPlan)
        {
            if (!deadline.TryGetRemainingTimeout(out var planTimeout))
            {
                return CreateFailure(
                    RequestFailureNormalizer.FromTransportFailure(
                        ExecutionErrorCodes.IpcTimeout,
                        "Timed out before Unity IPC plan request could begin."),
                    baseOutput);
            }

            var planExecutionResult = await unityIpcRequestExecutor.ExecuteAsync(
                    UcliCommandIds.Call,
                    mode,
                    planTimeout,
                    preparedRequest.Config,
                    preparedRequest.UnityProject,
                    CreateExecuteRequestPayload(
                        preparedRequest.RequestJson,
                        UcliCommandIds.Plan,
                        input.FailFast,
                        input.AllowDangerous),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!planExecutionResult.IsSuccess)
            {
                var failure = RequestFailureNormalizer.FromUnityRequestFailure(planExecutionResult.FailureInfo!);
                return CreateFailure(
                    failure,
                    baseOutput);
            }

            var convertedPlanResponse = ExecuteResponseConverter.Convert(planExecutionResult.Response!);
            var planOutput = new CallPlanOutput(
                RequestId: preparedRequest.Request.RequestId!,
                OpResults: convertedPlanResponse.OpResults,
                PlanToken: convertedPlanResponse.PlanToken);
            baseOutput = baseOutput with { Plan = planOutput };

            if (!convertedPlanResponse.IsSuccess)
            {
                var failures = RequestFailureNormalizer.FromOperationErrors(convertedPlanResponse.Errors, "uCLI call pre-plan failed.");
                return CallServiceResult.Failure(
                    RequestFailureNormalizer.ResolveMessage(failures, "uCLI call pre-plan failed."),
                    failures,
                    baseOutput);
            }

            if (string.IsNullOrWhiteSpace(convertedPlanResponse.PlanToken))
            {
                return CreateFailure(
                    RequestFailureNormalizer.FromTransportFailure(
                        UcliCoreErrorCodes.InternalError,
                        "Execute response payload is invalid. The 'planToken' field is missing."),
                    baseOutput);
            }

            if (effectivePlanToken is null)
            {
                effectivePlanToken = convertedPlanResponse.PlanToken;
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var callTimeout))
        {
            return CreateFailure(
                RequestFailureNormalizer.FromTransportFailure(
                    ExecutionErrorCodes.IpcTimeout,
                    "Timed out before Unity IPC call request could begin."),
                baseOutput);
        }

        var callExecutionResult = await unityIpcRequestExecutor.ExecuteAsync(
                UcliCommandIds.Call,
                mode,
                callTimeout,
                preparedRequest.Config,
                preparedRequest.UnityProject,
                CreateExecuteRequestPayload(
                    preparedRequest.RequestJson,
                    UcliCommandIds.Call,
                    input.FailFast,
                    input.AllowDangerous,
                    effectivePlanToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (!callExecutionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(callExecutionResult.FailureInfo!);
            return CreateFailure(
                failure,
                baseOutput);
        }

        var convertedCallResponse = ExecuteResponseConverter.Convert(callExecutionResult.Response!);
        var executionOutput = baseOutput with
        {
            OpResults = convertedCallResponse.OpResults,
            ReadPostcondition = convertedCallResponse.ReadPostcondition,
        };
        var postprocessedCallResponse = await ExecuteResponseReadPostconditionProcessor.PersistAsync(
                convertedCallResponse,
                mutationReadPostconditionStore,
                preparedRequest.UnityProject.RepositoryRoot,
                preparedRequest.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        convertedCallResponse = postprocessedCallResponse.Response;
        if (postprocessedCallResponse.PersistenceError != null)
        {
            var failures = RequestFailureNormalizer.FromOperationErrors(convertedCallResponse.Errors, "uCLI call failed.");
            return CallServiceResult.Failure(
                postprocessedCallResponse.PersistenceError.Message,
                failures,
                executionOutput);
        }

        if (!convertedCallResponse.IsSuccess)
        {
            var failures = RequestFailureNormalizer.FromOperationErrors(convertedCallResponse.Errors, "uCLI call failed.");
            return CallServiceResult.Failure(
                RequestFailureNormalizer.ResolveMessage(failures, "uCLI call failed."),
                failures,
                executionOutput);
        }

        return CallServiceResult.Success(
            executionOutput,
            "uCLI call completed.");
    }

    private static UnityRequestPayload CreateExecuteRequestPayload (
        string requestJson,
        UcliCommand command,
        bool failFast,
        bool allowDangerous = false,
        string? planToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        using var document = JsonDocument.Parse(requestJson);
        return new UnityRequestPayload.ExecuteJson(command, document.RootElement.Clone(), failFast, allowDangerous, planToken);
    }

    private static CallServiceResult CreateFailure (
        ApplicationFailure error,
        CallExecutionOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return CallServiceResult.Failure(
            error.Message,
            [
                error,
            ],
            output);
    }

}
