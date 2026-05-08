using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Postprocessing;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Executes fixed operations by authorizing one embedded operation descriptor and dispatching it through Unity IPC. </summary>
internal sealed class OperationExecuteService : IOperationExecuteService
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IOperationAuthorizationService operationAuthorizationService;

    private readonly IUnityRequestExecutor unityIpcRequestExecutor;

    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="OperationExecuteService" /> class. </summary>
    /// <param name="projectContextResolver"> The shared project-context resolver dependency. </param>
    /// <param name="operationAuthorizationService"> The operation authorization dependency. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public OperationExecuteService (
        IProjectContextResolver projectContextResolver,
        IOperationAuthorizationService operationAuthorizationService,
        IUnityRequestExecutor unityIpcRequestExecutor,
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        TimeProvider? timeProvider = null)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.operationAuthorizationService = operationAuthorizationService ?? throw new ArgumentNullException(nameof(operationAuthorizationService));
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<OperationExecuteResult> Execute (
        OperationExecuteDefinition definition,
        OperationExecuteInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(input);

        var requestId = Guid.NewGuid().ToString("D");

        var projectContextResult = await projectContextResolver.Resolve(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return OperationExecuteResultFactory.FromExecutionError(requestId, projectContextResult.Error!, definition.FailureMessage);
        }

        var projectContext = projectContextResult.Context!;
        var config = projectContext.Config;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            definition.Command,
            config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return OperationExecuteResultFactory.FromExecutionError(requestId, timeoutResolutionResult.Error!, definition.FailureMessage);
        }

        var deadline = ExecutionDeadline.Start(timeoutResolutionResult.Timeout!.Value, timeProvider);
        var executionMode = input.Mode ?? UnityExecutionMode.Auto;

        var authorizationResult = await operationAuthorizationService.Authorize(
                definition.Descriptor,
                config,
                cancellationToken)
            .ConfigureAwait(false);
        if (!authorizationResult.IsAllowed)
        {
            return OperationExecuteResultFactory.FromValidationErrors(
                requestId,
                [
                    new ValidationError(
                        authorizationResult.ErrorCode ?? ValidationErrorCodes.OperationNotAllowed,
                        authorizationResult.Message,
                        definition.OperationId),
                ],
                definition.FailureMessage);
        }

        string? planToken = null;
        if (config.PlanTokenMode == PlanTokenMode.Required)
        {
            if (!deadline.TryGetRemainingTimeout(out var planTimeout))
            {
                return OperationExecuteResultFactory.FromExecutionError(
                    requestId,
                    ExecutionError.Timeout("Timed out before Unity IPC plan request could begin."),
                    definition.FailureMessage);
            }

            var planTokenResult = await IssuePlanToken(
                    definition,
                    requestId,
                    executionMode,
                    planTimeout,
                    input.FailFast,
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

        if (!deadline.TryGetRemainingTimeout(out var executeTimeout))
        {
            return OperationExecuteResultFactory.FromExecutionError(
                requestId,
                ExecutionError.Timeout("Timed out before Unity IPC execute request could begin."),
                definition.FailureMessage);
        }

        var executionResult = await unityIpcRequestExecutor.Execute(
                definition.Command,
                executionMode,
                executeTimeout,
                config,
                projectContext.UnityProject,
                new UnityRequestPayload.ExecuteOperation(
                    UcliCommandIds.Call,
                    requestId,
                    definition.OperationId,
                    definition.Descriptor.Name,
                    definition.Args,
                    input.FailFast,
                    planToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestServiceResultPolicy.FromUnityRequestFailure(executionResult.FailureInfo!);
            return OperationExecuteResultFactory.Failure(
                requestId,
                [],
                [
                    failure.Error,
                ],
                definition.FailureMessage);
        }

        var postprocessedResponse = await ExecuteResponseReadPostconditionProcessor.Persist(
                ExecuteResponseConverter.Convert(executionResult.Response!),
                mutationReadPostconditionStore,
                projectContext.UnityProject.RepositoryRoot,
                projectContext.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var convertedResponse = postprocessedResponse.Response;

        if (convertedResponse.IsSuccess)
        {
            return OperationExecuteResultFactory.Success(
                requestId,
                convertedResponse.OpResults,
                definition.SuccessMessage,
                convertedResponse.ReadPostcondition);
        }

        return OperationExecuteResultFactory.Failure(
            requestId,
            convertedResponse.OpResults,
            RequestServiceResultPolicy.FromOperationErrors(convertedResponse.Errors, definition.FailureMessage),
            definition.FailureMessage,
            convertedResponse.ReadPostcondition);
    }

    /// <summary> Executes one internal <c>plan</c> pass and returns the issued plan token. </summary>
    /// <param name="definition"> The fixed operation definition. </param>
    /// <param name="requestId"> The generated request identifier. </param>
    /// <param name="mode"> The normalized Unity execution mode. </param>
    /// <param name="timeout"> The remaining timeout budget for this internal plan pass. </param>
    /// <param name="failFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="config"> The resolved CLI configuration. </param>
    /// <param name="unityProject"> The resolved Unity project. </param>
    /// <param name="cancellationToken"> The propagated cancellation token. </param>
    /// <returns> One tuple containing the issued plan token, or a normalized failure result when plan execution cannot continue. </returns>
    private async ValueTask<(string? PlanToken, OperationExecuteResult? FailureResult)> IssuePlanToken (
        OperationExecuteDefinition definition,
        string requestId,
        UnityExecutionMode mode,
        TimeSpan timeout,
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
                new UnityRequestPayload.ExecuteOperation(
                    UcliCommandIds.Plan,
                    requestId,
                    definition.OperationId,
                    definition.Descriptor.Name,
                    definition.Args,
                    failFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestServiceResultPolicy.FromUnityRequestFailure(executionResult.FailureInfo!);
            return (
                null,
                OperationExecuteResultFactory.Failure(
                    requestId,
                    [],
                    [
                        failure.Error,
                    ],
                    definition.FailureMessage));
        }

        var convertedResponse = ExecuteResponseConverter.Convert(executionResult.Response!);
        if (!convertedResponse.IsSuccess)
        {
            return (
                null,
                OperationExecuteResultFactory.Failure(
                    requestId,
                    convertedResponse.OpResults,
                    RequestServiceResultPolicy.FromOperationErrors(convertedResponse.Errors, definition.FailureMessage),
                    definition.FailureMessage));
        }

        if (string.IsNullOrWhiteSpace(convertedResponse.PlanToken))
        {
            return (
                null,
                OperationExecuteResultFactory.Failure(
                    requestId,
                    convertedResponse.OpResults,
                    [
                        RequestServiceResultPolicy.FromTransportFailure(
                            UcliCoreErrorCodes.InternalError,
                            "Execute response payload is invalid. The 'planToken' field is missing."),
                    ],
                    definition.FailureMessage));
        }

        return (convertedResponse.PlanToken, null);
    }

}
