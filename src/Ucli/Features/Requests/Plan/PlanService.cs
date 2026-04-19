using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Features.Requests.Plan;

/// <summary> Implements the <c>plan</c> workflow by combining static-validation preflight and Unity IPC plan execution. </summary>
internal sealed class PlanService : IPlanService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidationPreflightService requestStaticValidationPreflightService;

    private readonly IUnityIpcRequestExecutor unityIpcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlanService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidationPreflightService"> The shared static-validation preflight dependency. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlanService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidationPreflightService requestStaticValidationPreflightService,
        IUnityIpcRequestExecutor unityIpcRequestExecutor)
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
                input.RequestPath,
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!requestPreparationResult.IsSuccess)
        {
            var error = requestPreparationResult.Error!;
            return CreateFailure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                error.Kind == ExecutionErrorKind.InvalidArgument
                    ? (int)CliExitCode.InvalidArgument
                    : (int)CliExitCode.ToolError);
        }

        var requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.Prepare(
                requestPreparationResult.PreparedRequest!,
                input.ReadIndexMode,
                cancellationToken)
            .ConfigureAwait(false);

        var preparedRequest = requestStaticValidationPreflightResult.PreparedRequest;
        var baseOutput = TryCreateBaseOutput(preparedRequest, requestStaticValidationPreflightResult.ReadIndex);
        if (requestStaticValidationPreflightResult.Error != null)
        {
            return CreateFailure(
                requestStaticValidationPreflightResult.Error.Message,
                requestStaticValidationPreflightResult.ErrorCode!,
                ExecuteResponseConverter.ResolveExitCode(requestStaticValidationPreflightResult.ErrorCode!),
                baseOutput);
        }

        if (requestStaticValidationPreflightResult.HasValidationErrors)
        {
            return PlanServiceResult.Failure(
                "Static validation failed.",
                ConvertValidationErrors(requestStaticValidationPreflightResult.ValidationErrors),
                (int)CliExitCode.InvalidArgument,
                baseOutput);
        }

        if (preparedRequest == null)
        {
            throw new InvalidOperationException("Prepared request must be available when static-validation preflight succeeds.");
        }
        if (baseOutput == null)
        {
            throw new InvalidOperationException("Plan output must be available when static-validation preflight succeeds.");
        }

        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Plan,
            preparedRequest.ProjectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            var error = timeoutResolutionResult.Error!;
            return CreateFailure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                error.Kind == ExecutionErrorKind.InvalidArgument
                    ? (int)CliExitCode.InvalidArgument
                    : (int)CliExitCode.ToolError,
                baseOutput);
        }

        var executionMode = input.Mode ?? UnityExecutionMode.Auto;

        var executionResult = await unityIpcRequestExecutor.Execute(
                UcliCommandIds.Plan,
                executionMode,
                timeoutResolutionResult.Timeout!.Value,
                preparedRequest.ProjectContext.Config,
                preparedRequest.ProjectContext.UnityProject,
                IpcMethodNames.Execute,
                CreateExecuteRequestPayload(preparedRequest.RequestJson, input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var errorCode = ResolveErrorCode(executionResult.ErrorCode);
            return CreateFailure(
                executionResult.Message,
                errorCode,
                ExecuteResponseConverter.ResolveExitCode(errorCode),
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
                ResolveFailureMessage(convertedResponse.Errors, "uCLI plan failed."),
                convertedResponse.Errors,
                convertedResponse.ExitCode,
                executionOutput);
        }

        if (string.IsNullOrWhiteSpace(convertedResponse.PlanToken))
        {
            return CreateFailure(
                "Execute response payload is invalid. The 'planToken' field is missing.",
                IpcErrorCodes.InternalError,
                (int)CliExitCode.ToolError,
                executionOutput);
        }

        return PlanServiceResult.Success(
            executionOutput with
            {
                PlanToken = convertedResponse.PlanToken,
            },
            "uCLI plan completed.");
    }

    private static PlanExecutionOutput? TryCreateBaseOutput (
        PreparedRequestContext? preparedRequest,
        ReadIndexInfo? readIndex)
    {
        if (preparedRequest == null
            || readIndex == null
            || string.IsNullOrWhiteSpace(preparedRequest.Request.RequestId))
        {
            return null;
        }

        return new PlanExecutionOutput(
            RequestId: preparedRequest.Request.RequestId,
            OpResults: [],
            ReadIndex: readIndex,
            PlanToken: null);
    }

    private static JsonElement CreateExecuteRequestPayload (
        string requestJson,
        bool failFast)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        using var document = JsonDocument.Parse(requestJson);
        return ExecuteRequestPayloadFactory.Create(
            UcliCommandIds.Plan,
            document.RootElement.Clone(),
            failFast);
    }

    private static PlanServiceResult CreateFailure (
        string message,
        string errorCode,
        int exitCode,
        PlanExecutionOutput? output = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return PlanServiceResult.Failure(
            message,
            [
                new IpcError(errorCode, message, null),
            ],
            exitCode,
            output);
    }

    private static IReadOnlyList<IpcError> ConvertValidationErrors (IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        var errors = new IpcError[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            errors[i] = new IpcError(validationError.Code, validationError.Message, validationError.OpId);
        }

        return errors;
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
}