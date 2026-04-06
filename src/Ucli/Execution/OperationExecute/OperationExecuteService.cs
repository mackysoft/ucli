using System.Globalization;
using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution.OperationExecute;

/// <summary> Executes fixed operations by authorizing one embedded operation descriptor and dispatching it through Unity IPC. </summary>
internal sealed class OperationExecuteService : IOperationExecuteService
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IOperationAuthorizationService operationAuthorizationService;

    private readonly IUnityIpcRequestExecutor unityIpcRequestExecutor;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="OperationExecuteService" /> class. </summary>
    /// <param name="projectContextResolver"> The shared project-context resolver dependency. </param>
    /// <param name="operationAuthorizationService"> The operation authorization dependency. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public OperationExecuteService (
        IProjectContextResolver projectContextResolver,
        IOperationAuthorizationService operationAuthorizationService,
        IUnityIpcRequestExecutor unityIpcRequestExecutor,
        TimeProvider? timeProvider = null)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.operationAuthorizationService = operationAuthorizationService ?? throw new ArgumentNullException(nameof(operationAuthorizationService));
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<OperationExecuteResult> Execute (
        OperationExecuteDefinition definition,
        string? projectPath,
        string? mode,
        string? timeout,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(definition);

        var requestId = Guid.NewGuid().ToString("D");

        var projectContextResult = await projectContextResolver.Resolve(projectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return CreateFailureFromExecutionError(requestId, projectContextResult.Error!);
        }

        var projectContext = projectContextResult.Context!;
        var config = projectContext.Config;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(timeout, definition.Command, config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return CreateFailureFromExecutionError(requestId, timeoutResolutionResult.Error!);
        }

        var deadline = ExecutionDeadline.Start(timeoutResolutionResult.Timeout!.Value, timeProvider);

        var authorizationResult = await operationAuthorizationService.Authorize(
                definition.Descriptor,
                config,
                cancellationToken)
            .ConfigureAwait(false);
        if (!authorizationResult.IsAllowed)
        {
            return CreateValidationFailure(
                requestId,
                [
                    new ValidationError(
                        authorizationResult.ErrorCode ?? ValidationErrorCodes.OperationNotAllowed,
                        authorizationResult.Message,
                        definition.OperationId),
                ]);
        }

        string? planToken = null;
        if (config.PlanTokenMode == PlanTokenMode.Required)
        {
            if (!TryGetRemainingTimeoutOption(deadline, out var planTimeoutOption))
            {
                return CreateFailureFromExecutionError(
                    requestId,
                    ExecutionError.Timeout("Timed out before Unity IPC plan request could begin."));
            }

            var planTokenResult = await IssuePlanToken(
                    definition,
                    requestId,
                    mode,
                    planTimeoutOption,
                    failFast,
                    config,
                    projectContext.UnityProject,
                    cancellationToken)
                .ConfigureAwait(false);
            if (planTokenResult.FailureResult != null)
            {
                return planTokenResult.FailureResult;
            }

            planToken = planTokenResult.PlanToken;
        }

        if (!TryGetRemainingTimeoutOption(deadline, out var executeTimeoutOption))
        {
            return CreateFailureFromExecutionError(
                requestId,
                ExecutionError.Timeout("Timed out before Unity IPC execute request could begin."));
        }

        var executionResult = await unityIpcRequestExecutor.Execute(
                definition.Command,
                mode,
                executeTimeoutOption,
                config,
                projectContext.UnityProject,
                IpcMethodNames.Execute,
                CreateExecuteRequestPayload(definition, requestId, UcliCommandIds.Call, failFast, planToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var errorCode = ResolveErrorCode(executionResult.ErrorCode);
            return CreateResult(
                requestId,
                [],
                [
                    new IpcError(errorCode, executionResult.Message, null),
                ],
                ResolveExitCode(errorCode));
        }

        return CreateFromIpcResponse(requestId, executionResult.Response!);
    }

    /// <summary> Executes one internal <c>plan</c> pass and returns the issued plan token. </summary>
    /// <param name="definition"> The fixed operation definition. </param>
    /// <param name="requestId"> The generated request identifier. </param>
    /// <param name="mode"> The optional Unity execution mode. </param>
    /// <param name="timeout"> The optional timeout in milliseconds. </param>
    /// <param name="failFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="config"> The resolved CLI configuration. </param>
    /// <param name="unityProject"> The resolved Unity project. </param>
    /// <param name="cancellationToken"> The propagated cancellation token. </param>
    /// <returns> One tuple containing the issued plan token, or a normalized failure result when plan execution cannot continue. </returns>
    private async ValueTask<(string? PlanToken, OperationExecuteResult? FailureResult)> IssuePlanToken (
        OperationExecuteDefinition definition,
        string requestId,
        string? mode,
        string? timeout,
        bool failFast,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(unityProject);

        var executionResult = await unityIpcRequestExecutor.Execute(
                definition.Command,
                mode,
                timeout,
                config,
                unityProject,
                IpcMethodNames.Execute,
                CreateExecuteRequestPayload(definition, requestId, UcliCommandIds.Plan, failFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var errorCode = ResolveErrorCode(executionResult.ErrorCode);
            return (
                null,
                CreateResult(
                    requestId,
                    [],
                    [
                        new IpcError(errorCode, executionResult.Message, null),
                    ],
                    ResolveExitCode(errorCode)));
        }

        var response = executionResult.Response!;
        if (!TryReadExecuteResponsePayload(requestId, response, out var payload, out var failureResult))
        {
            return (null, failureResult);
        }

        var normalizedErrors = NormalizeErrors(response.Status, response.Errors);
        if (normalizedErrors.Count != 0)
        {
            return (
                null,
                CreateResult(
                    requestId,
                    payload!.OpResults,
                    normalizedErrors,
                    ResolveExitCode(normalizedErrors)));
        }

        if (string.IsNullOrWhiteSpace(payload!.PlanToken))
        {
            return (
                null,
                CreateResult(
                    requestId,
                    payload.OpResults,
                    [
                        new IpcError(
                            IpcErrorCodes.InternalError,
                            "Execute response payload is invalid. The 'planToken' field is missing.",
                            null),
                    ],
                    (int)CliExitCode.ToolError));
        }

        return (payload.PlanToken, null);
    }

    private static bool TryGetRemainingTimeoutOption (
        ExecutionDeadline deadline,
        out string? timeout)
    {
        var remainingMilliseconds = deadline.GetRemainingWaitMilliseconds();
        if (remainingMilliseconds <= 0)
        {
            timeout = null;
            return false;
        }

        timeout = remainingMilliseconds.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary> Creates the execute payload for one fixed operation execution. </summary>
    /// <param name="definition"> The fixed operation definition. </param>
    /// <param name="requestId"> The generated request identifier. </param>
    /// <param name="command"> The internal execute command sent to Unity. </param>
    /// <param name="failFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="planToken"> The optional plan token attached to call execution. </param>
    /// <returns> The serialized IPC execute payload. </returns>
    private static JsonElement CreateExecuteRequestPayload (
        OperationExecuteDefinition definition,
        string requestId,
        UcliCommand command,
        bool failFast,
        string? planToken = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        var executeArguments = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            requestId,
            steps = new[]
            {
                new
                {
                    kind = "op",
                    id = definition.OperationId,
                    op = definition.Descriptor.Name,
                    args = definition.Args,
                },
            },
        });

        return IpcPayloadCodec.SerializeToElement(new IpcExecuteRequest(command, executeArguments)
        {
            FailFast = failFast,
            PlanToken = planToken,
        });
    }

    /// <summary> Creates one normalized result from one Unity IPC response. </summary>
    /// <param name="requestId"> The generated request identifier. </param>
    /// <param name="response"> The Unity IPC response. </param>
    /// <returns> The normalized operation execution result. </returns>
    private static OperationExecuteResult CreateFromIpcResponse (
        string requestId,
        IpcResponse response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(response);

        if (!TryReadExecuteResponsePayload(requestId, response, out var payload, out var failureResult))
        {
            return failureResult!;
        }

        var normalizedErrors = NormalizeErrors(response.Status, response.Errors);
        return CreateResult(
            requestId,
            payload!.OpResults,
            normalizedErrors,
            ResolveExitCode(normalizedErrors));
    }

    /// <summary> Reads the execute response payload and converts payload-shape failures into normalized CLI results. </summary>
    /// <param name="requestId"> The generated request identifier. </param>
    /// <param name="response"> The Unity IPC response. </param>
    /// <param name="payload"> The decoded execute payload when successful. </param>
    /// <param name="failureResult"> The normalized failure result when decoding fails. </param>
    /// <returns> <see langword="true" /> when the payload can be consumed; otherwise <see langword="false" />. </returns>
    private static bool TryReadExecuteResponsePayload (
        string requestId,
        IpcResponse response,
        out IpcExecuteResponse? payload,
        out OperationExecuteResult? failureResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(response);

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcExecuteResponse? deserializedPayload, out var payloadError))
        {
            payload = null;
            failureResult = CreateResult(
                requestId,
                [],
                [
                    new IpcError(
                        IpcErrorCodes.InternalError,
                        $"Execute response payload is invalid. {payloadError.Message}",
                        null),
                ],
                (int)CliExitCode.ToolError);
            return false;
        }

        if (deserializedPayload == null || deserializedPayload.OpResults is null)
        {
            payload = null;
            failureResult = CreateResult(
                requestId,
                [],
                [
                    new IpcError(
                        IpcErrorCodes.InternalError,
                        "Execute response payload is invalid. The 'opResults' field is missing.",
                        null),
                ],
                (int)CliExitCode.ToolError);
            return false;
        }

        payload = deserializedPayload;
        failureResult = null;
        return true;
    }

    /// <summary> Creates one failure result from one structured execution error. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The normalized operation execution result. </returns>
    private static OperationExecuteResult CreateFailureFromExecutionError (
        string requestId,
        ExecutionError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(error);

        var errorCode = ExecutionErrorKindCodeMapper.ToCode(error.Kind);
        return CreateResult(
            requestId,
            [],
            [
                new IpcError(errorCode, error.Message, null),
            ],
            error.Kind == ExecutionErrorKind.InvalidArgument
                ? (int)CliExitCode.InvalidArgument
                : (int)CliExitCode.ToolError);
    }

    /// <summary> Creates one failure result from static validation errors. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="validationErrors"> The static validation errors. </param>
    /// <returns> The normalized operation execution result. </returns>
    private static OperationExecuteResult CreateValidationFailure (
        string requestId,
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(validationErrors);

        var errors = new IpcError[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            errors[i] = new IpcError(validationError.Code, validationError.Message, validationError.OpId);
        }

        return CreateResult(
            requestId,
            [],
            errors,
            (int)CliExitCode.InvalidArgument);
    }

    /// <summary> Creates one normalized operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <param name="exitCode"> The associated process exit code. </param>
    /// <returns> The normalized operation execution result. </returns>
    private static OperationExecuteResult CreateResult (
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        int exitCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(errors);

        return new OperationExecuteResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            OpResults: opResults,
            Errors: errors,
            ExitCode: exitCode);
    }

    /// <summary> Normalizes the response error collection so failed responses always expose at least one error. </summary>
    /// <param name="status"> The protocol status returned from Unity. </param>
    /// <param name="errors"> The Unity response errors. </param>
    /// <returns> The normalized error collection. </returns>
    private static IReadOnlyList<IpcError> NormalizeErrors (
        string? status,
        IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (string.Equals(status, IpcProtocol.StatusOk, StringComparison.Ordinal)
            && errors.Count == 0)
        {
            return [];
        }

        if (errors.Count != 0)
        {
            return errors;
        }

        return
        [
            new IpcError(
                IpcErrorCodes.InternalError,
                $"Execute response failed with status '{status}'.",
                null),
        ];
    }

    /// <summary> Resolves the machine-readable error code used for transport-level failures. </summary>
    /// <param name="errorCode"> The raw error code. </param>
    /// <returns> The normalized error code. </returns>
    private static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
    }

    /// <summary> Resolves the CLI exit code from one transport-level error code. </summary>
    /// <param name="errorCode"> The raw error code. </param>
    /// <returns> The associated CLI exit code. </returns>
    private static int ResolveExitCode (string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return string.Equals(errorCode, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
            ? (int)CliExitCode.InvalidArgument
            : (int)CliExitCode.ToolError;
    }

    /// <summary> Resolves the CLI exit code from one machine-readable error collection. </summary>
    /// <param name="errors"> The machine-readable error collection. </param>
    /// <returns> The associated CLI exit code. </returns>
    private static int ResolveExitCode (IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            return (int)CliExitCode.Success;
        }

        return errors.All(static error => string.Equals(error.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            ? (int)CliExitCode.InvalidArgument
            : (int)CliExitCode.ToolError;
    }
}