using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Postprocessing;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
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
        Guid requestId,
        PhaseExecutionPreparedRequest preparedRequest,
        UnityExecutionMode mode,
        CallCommandInput input,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(deadline);

        var baseOutput = CallExecutionOutputFactory.CreateBase(requestId, preparedRequest.PreparedRequest);
        var effectivePlanToken = StringValueNormalizer.TrimToNull(input.PlanToken);
        var executionOwnerCommand = input.ExecutionOwnerCommand;

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
                    executionOwnerCommand,
                    mode,
                    planTimeout,
                    preparedRequest.Config,
                    preparedRequest.UnityProject,
                    CreateExecuteRequestPayload(
                        preparedRequest.RequestJson,
                        UcliCommandIds.Plan,
                        input.FailFast,
                        input.AllowDangerous,
                        allowPlayMode: input.AllowPlayMode),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!planExecutionResult.IsSuccess)
            {
                var failure = RequestFailureNormalizer.FromUnityRequestFailure(planExecutionResult.FailureInfo!);
                return CreateFailure(
                    failure,
                    baseOutput);
            }

            var convertedPlanResponse = ExecuteResponseConverter.Convert(
                planExecutionResult.Response!,
                preparedRequest.UnityProject);
            var planProject = convertedPlanResponse.Project ?? baseOutput.Project;
            var planOutput = new CallPlanOutput(
                opResults: convertedPlanResponse.OpResults,
                planToken: convertedPlanResponse.PlanToken)
            {
                ContractViolations = convertedPlanResponse.ContractViolations,
            };
            baseOutput = baseOutput with
            {
                Project = planProject,
                ContractViolations = convertedPlanResponse.ContractViolations,
                Plan = planOutput,
            };

            if (!convertedPlanResponse.IsSuccess)
            {
                var prePlanFailureMessage = CreatePrePlanFailureMessage(executionOwnerCommand);
                var failures = RequestFailureNormalizer.FromOperationErrors(convertedPlanResponse.Errors);
                return CallServiceResult.Failure(
                    RequestFailureNormalizer.ResolveMessage(failures, prePlanFailureMessage),
                    failures,
                    baseOutput);
            }

            if (convertedPlanResponse.PlanToken == null)
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
                executionOwnerCommand,
                mode,
                callTimeout,
                preparedRequest.Config,
                preparedRequest.UnityProject,
                CreateExecuteRequestPayload(
                    preparedRequest.RequestJson,
                    UcliCommandIds.Call,
                    input.FailFast,
                    input.AllowDangerous,
                    effectivePlanToken,
                    input.AllowPlayMode),
                cancellationToken)
            .ConfigureAwait(false);
        if (!callExecutionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(callExecutionResult.FailureInfo!);
            return CreateFailure(
                failure,
                baseOutput);
        }

        var convertedCallResponse = ExecuteResponseConverter.Convert(
            callExecutionResult.Response!,
            preparedRequest.UnityProject);
        var callProject = convertedCallResponse.Project ?? baseOutput.Project;
        var executionOutput = baseOutput with
        {
            Project = callProject,
            OpResults = convertedCallResponse.OpResults,
            ContractViolations = convertedCallResponse.ContractViolations,
            ReadPostcondition = convertedCallResponse.ReadPostcondition,
            PostReadSource = convertedCallResponse.PostReadSource,
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
            var callFailureMessage = CreateCallFailureMessage(executionOwnerCommand);
            var failures = RequestFailureNormalizer.FromOperationErrors(convertedCallResponse.Errors);
            return CallServiceResult.Failure(
                postprocessedCallResponse.PersistenceError.Message,
                failures,
                executionOutput);
        }

        if (!convertedCallResponse.IsSuccess)
        {
            var callFailureMessage = CreateCallFailureMessage(executionOwnerCommand);
            var failures = RequestFailureNormalizer.FromOperationErrors(convertedCallResponse.Errors);
            return CallServiceResult.Failure(
                RequestFailureNormalizer.ResolveMessage(failures, callFailureMessage),
                failures,
                executionOutput);
        }

        return CallServiceResult.Success(
            executionOutput,
            CreateSuccessMessage(executionOwnerCommand));
    }

    private static string CreatePrePlanFailureMessage (UcliCommand command)
    {
        return $"uCLI {command.Name} pre-plan failed.";
    }

    private static string CreateCallFailureMessage (UcliCommand command)
    {
        return $"uCLI {command.Name} failed.";
    }

    private static string CreateSuccessMessage (UcliCommand command)
    {
        return $"uCLI {command.Name} completed.";
    }

    private static UnityRequestPayload CreateExecuteRequestPayload (
        string requestJson,
        UcliCommand command,
        bool failFast,
        bool allowDangerous = false,
        string? planToken = null,
        bool allowPlayMode = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        ArgumentNullException.ThrowIfNull(command);

        using var document = JsonDocument.Parse(requestJson);
        return new UnityRequestPayload.ExecuteJson(command, document.RootElement.Clone(), failFast, allowDangerous, planToken, allowPlayMode);
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
