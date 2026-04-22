using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Call.UseCases.Call.Projection;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Features.Requests.Call.UseCases.Call;

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
    public async ValueTask<CallServiceResult> Execute (
        PhaseExecutionPreparedRequest preparedRequest,
        UnityExecutionMode mode,
        CallCommandInput input,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(input);

        var baseOutput = CallExecutionOutputFactory.CreateBase(preparedRequest.Request.RequestId);
        var effectivePlanToken = StringValueNormalizer.TrimToNull(input.PlanToken);

        if (input.WithPlan)
        {
            if (!deadline.TryGetRemainingTimeout(out var planTimeout))
            {
                return CreateFailure(
                    "Timed out before Unity IPC plan request could begin.",
                    ExecutionErrorCodes.IpcTimeout,
                    (int)CliExitCode.ToolError,
                    baseOutput);
            }

            var planExecutionResult = await unityIpcRequestExecutor.Execute(
                    UcliCommandIds.Call,
                    mode,
                    planTimeout,
                    preparedRequest.Config,
                    preparedRequest.UnityProject,
                    IpcMethodNames.Execute,
                    CreateExecuteRequestPayload(
                        preparedRequest.RequestJson,
                        UcliCommandIds.Plan,
                        input.FailFast),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!planExecutionResult.IsSuccess)
            {
                var errorCode = ResolveErrorCode(planExecutionResult.ErrorCode);
                return CreateFailure(
                    planExecutionResult.Message,
                    errorCode,
                    ExecuteResponseConverter.ResolveExitCode(errorCode),
                    baseOutput);
            }

            var convertedPlanResponse = ExecuteResponseConverter.Convert(planExecutionResult.Response!);
            var planOutput = new CallPlanOutput(
                RequestId: preparedRequest.Request.RequestId!,
                OpResults: convertedPlanResponse.OpResults,
                PlanToken: convertedPlanResponse.PlanToken);
            baseOutput = baseOutput is null
                ? null
                : baseOutput with { Plan = planOutput };

            if (!convertedPlanResponse.IsSuccess)
            {
                return CallServiceResult.Failure(
                    ResolveFailureMessage(convertedPlanResponse.Errors, "uCLI call pre-plan failed."),
                    convertedPlanResponse.Errors,
                    convertedPlanResponse.ExitCode,
                    baseOutput);
            }

            if (string.IsNullOrWhiteSpace(convertedPlanResponse.PlanToken))
            {
                return CreateFailure(
                    "Execute response payload is invalid. The 'planToken' field is missing.",
                    IpcErrorCodes.InternalError,
                    (int)CliExitCode.ToolError,
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
                "Timed out before Unity IPC call request could begin.",
                ExecutionErrorCodes.IpcTimeout,
                (int)CliExitCode.ToolError,
                baseOutput);
        }

        var callExecutionResult = await unityIpcRequestExecutor.Execute(
                UcliCommandIds.Call,
                mode,
                callTimeout,
                preparedRequest.Config,
                preparedRequest.UnityProject,
                IpcMethodNames.Execute,
                CreateExecuteRequestPayload(
                    preparedRequest.RequestJson,
                    UcliCommandIds.Call,
                    input.FailFast,
                    effectivePlanToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (!callExecutionResult.IsSuccess)
        {
            var errorCode = ResolveErrorCode(callExecutionResult.ErrorCode);
            return CreateFailure(
                callExecutionResult.Message,
                errorCode,
                ExecuteResponseConverter.ResolveExitCode(errorCode),
                baseOutput);
        }

        var convertedCallResponse = ExecuteResponseConverter.Convert(callExecutionResult.Response!);
        var executionOutput = baseOutput is null
            ? null
            : baseOutput with
            {
                OpResults = convertedCallResponse.OpResults,
                ReadPostcondition = convertedCallResponse.ReadPostcondition,
            };
        var persistenceError = executionOutput == null
            ? null
            : await MutationReadPostconditionPersistence.WriteOrCreateError(
                    mutationReadPostconditionStore,
                    preparedRequest.UnityProject.RepositoryRoot,
                    preparedRequest.UnityProject.ProjectFingerprint,
                    convertedCallResponse.ReadPostcondition,
                    cancellationToken)
                .ConfigureAwait(false);
        if (persistenceError != null)
        {
            return CallServiceResult.Failure(
                persistenceError.Message,
                AppendError(convertedCallResponse.Errors, persistenceError),
                (int)CliExitCode.ToolError,
                executionOutput);
        }

        if (!convertedCallResponse.IsSuccess)
        {
            return CallServiceResult.Failure(
                ResolveFailureMessage(convertedCallResponse.Errors, "uCLI call failed."),
                convertedCallResponse.Errors,
                convertedCallResponse.ExitCode,
                executionOutput);
        }

        return CallServiceResult.Success(
            executionOutput ?? throw new InvalidOperationException("Successful call execution must produce an output payload."),
            "uCLI call completed.");
    }

    private static JsonElement CreateExecuteRequestPayload (
        string requestJson,
        UcliCommand command,
        bool failFast,
        string? planToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        using var document = JsonDocument.Parse(requestJson);
        return ExecuteRequestPayloadFactory.Create(command, document.RootElement.Clone(), failFast, planToken);
    }

    private static CallServiceResult CreateFailure (
        string message,
        string errorCode,
        int exitCode,
        CallExecutionOutput? output = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return CallServiceResult.Failure(
            message,
            [
                new IpcError(errorCode, message, null),
            ],
            exitCode,
            output);
    }

    private static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
    }

    private static string ResolveFailureMessage (
        IReadOnlyList<IpcError> errors,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackMessage);

        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }
        }

        return fallbackMessage;
    }

    private static IReadOnlyList<IpcError> AppendError (
        IReadOnlyList<IpcError> errors,
        IpcError persistenceError)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(persistenceError);

        var mergedErrors = new IpcError[errors.Count + 1];
        for (var i = 0; i < errors.Count; i++)
        {
            mergedErrors[i] = errors[i];
        }

        mergedErrors[^1] = persistenceError;
        return mergedErrors;
    }
}