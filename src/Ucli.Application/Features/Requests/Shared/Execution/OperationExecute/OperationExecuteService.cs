using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Postprocessing;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Executes fixed operations by authorizing one embedded operation descriptor and dispatching it through Unity IPC. </summary>
internal sealed class OperationExecuteService : IOperationExecuteService
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IOperationCatalog operationCatalog;

    private readonly IOperationAuthorizationService operationAuthorizationService;

    private readonly IUnityRequestExecutor unityIpcRequestExecutor;

    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="OperationExecuteService" /> class. </summary>
    /// <param name="projectContextResolver"> The shared project-context resolver dependency. </param>
    /// <param name="operationCatalog"> The authoritative operation-catalog dependency. </param>
    /// <param name="operationAuthorizationService"> The operation authorization dependency. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public OperationExecuteService (
        IProjectContextResolver projectContextResolver,
        IOperationCatalog operationCatalog,
        IOperationAuthorizationService operationAuthorizationService,
        IUnityRequestExecutor unityIpcRequestExecutor,
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
        this.operationAuthorizationService = operationAuthorizationService ?? throw new ArgumentNullException(nameof(operationAuthorizationService));
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<OperationExecuteResult> ExecuteAsync (
        Guid requestId,
        OperationExecuteDefinition definition,
        OperationExecuteInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(input);

        var projectContextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return OperationExecuteResultFactory.FromExecutionError(
                requestId,
                projectContextResult.Error!,
                project: null);
        }

        var projectContext = projectContextResult.Context!;
        var project = ProjectIdentityInfo.From(projectContext.UnityProject);
        var config = projectContext.Config;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            definition.Command,
            config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return OperationExecuteResultFactory.FromExecutionError(requestId, timeoutResolutionResult.Error!, project);
        }

        var deadline = ExecutionDeadline.Start(timeoutResolutionResult.Timeout!.Value, timeProvider);
        var executionMode = input.Mode ?? UnityExecutionMode.Auto;

        if (!deadline.TryGetRemainingTimeout(out var catalogTimeout))
        {
            return OperationExecuteResultFactory.FromExecutionError(
                requestId,
                ExecutionError.Timeout("Timed out before operation metadata discovery could begin.", ExecutionErrorCodes.IpcTimeout),
                project);
        }

        IReadOnlyList<UcliOperationDescriptor> operations;
        try
        {
            operations = await operationCatalog.GetAllAsync(
                    projectContext.UnityProject,
                    config,
                    executionMode,
                    catalogTimeout,
                    input.FailFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCatalogLoadException exception)
        {
            var failure = exception.CreatePrefixedFailure("Operation execution could not load operation metadata.");
            return OperationExecuteResultFactory.Failure(
                requestId,
                opResults: [],
                errors: [failure],
                contractViolations: [],
                readPostcondition: null,
                project: project,
                postReadSource: null);
        }
        catch (InvalidOperationException exception)
        {
            return OperationExecuteResultFactory.FromExecutionError(
                requestId,
                ExecutionError.InternalError(
                    $"Operation execution could not load operation metadata. {exception.Message}", UcliCoreErrorCodes.InternalError),
                project);
        }

        var descriptor = operations.FirstOrDefault(operation =>
            string.Equals(operation.Name, definition.OperationName, StringComparison.Ordinal));
        if (descriptor == null)
        {
            return OperationExecuteResultFactory.FromValidationErrors(
                requestId,
                [
                    new ValidationError(
                        ValidationErrorCodes.OperationNotFound,
                        $"Operation '{definition.OperationName}' is not registered.",
                        InstancePath: null),
                ],
                project);
        }

        var authorizationResult = await operationAuthorizationService.AuthorizeAsync(
                UcliOperationAuthorizationDescriptor.From(descriptor),
                config,
                cancellationToken)
            .ConfigureAwait(false);
        if (!authorizationResult.IsAllowed)
        {
            var denialCode = authorizationResult.ErrorCode
                ?? throw new InvalidOperationException(
                    "A denied operation authorization must contain an error code.");
            return OperationExecuteResultFactory.FromValidationErrors(
                requestId,
                [
                    new ValidationError(
                        denialCode,
                        authorizationResult.Message,
                        InstancePath: null),
                ],
                project);
        }

        string? planToken = null;
        if (config.PlanTokenMode == PlanTokenMode.Required)
        {
            if (!deadline.TryGetRemainingTimeout(out var planTimeout))
            {
                return OperationExecuteResultFactory.FromExecutionError(
                    requestId,
                    ExecutionError.Timeout("Timed out before Unity IPC plan request could begin.", ExecutionErrorCodes.IpcTimeout),
                    project);
            }

            var planTokenResult = await IssuePlanTokenAsync(
                    definition,
                    descriptor,
                    requestId,
                    executionMode,
                    planTimeout,
                    input.FailFast,
                    config,
                    projectContext.UnityProject,
                    project,
                    cancellationToken)
                .ConfigureAwait(false);
            switch (planTokenResult)
            {
                case PlanTokenIssueResult.Issued issued:
                    planToken = issued.PlanToken;
                    break;

                case PlanTokenIssueResult.Failed failed:
                    return failed.Result;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported plan-token issuance result '{planTokenResult.GetType().FullName}'.");
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var executeTimeout))
        {
            return OperationExecuteResultFactory.FromExecutionError(
                requestId,
                ExecutionError.Timeout("Timed out before Unity IPC execute request could begin.", ExecutionErrorCodes.IpcTimeout),
                project);
        }

        var executionResult = await unityIpcRequestExecutor.ExecuteAsync(
                definition.Command,
                executionMode,
                executeTimeout,
                config,
                projectContext.UnityProject,
                new UnityRequestPayload.ExecuteOperation(
                    UcliCommandIds.Call,
                    definition.OperationId,
                    descriptor.Name,
                    definition.Args,
                    input.FailFast,
                    PlanToken: planToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(executionResult.FailureInfo!);
            return OperationExecuteResultFactory.Failure(
                requestId,
                [],
                [
                    failure,
                ],
                contractViolations: [],
                readPostcondition: null,
                project,
                postReadSource: null);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(
            executionResult.Response!,
            projectContext.UnityProject);
        if (!TryValidateResponseContract(
                definition,
                descriptor,
                IpcExecuteOperationPhase.Call,
                convertedResponse,
                out var responseContractError))
        {
            return OperationExecuteResultFactory.FromExecutionError(
                requestId,
                ExecutionError.InternalError(responseContractError, UcliCoreErrorCodes.InternalError),
                project);
        }

        var postprocessedResponse = await ExecuteResponseReadPostconditionProcessor.PersistAsync(
                convertedResponse,
                mutationReadPostconditionStore,
                projectContext.UnityProject.RepositoryRoot,
                projectContext.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        convertedResponse = postprocessedResponse.Response;
        var responseProject = convertedResponse.Project ?? project;

        if (convertedResponse.IsSuccess)
        {
            return OperationExecuteResultFactory.Success(
                requestId,
                convertedResponse.OpResults,
                definition.SuccessMessage,
                convertedResponse.ReadPostcondition,
                responseProject,
                convertedResponse.ContractViolations,
                convertedResponse.PostReadSource);
        }

        return OperationExecuteResultFactory.Failure(
            requestId,
            convertedResponse.OpResults,
            RequestFailureNormalizer.FromOperationErrors(convertedResponse.Errors),
            contractViolations: convertedResponse.ContractViolations,
            readPostcondition: convertedResponse.ReadPostcondition,
            project: responseProject,
            postReadSource: convertedResponse.PostReadSource);
    }

    /// <summary> Executes one internal <c>plan</c> pass and returns the issued plan token. </summary>
    /// <param name="definition"> The fixed operation definition. </param>
    /// <param name="descriptor"> The authoritative operation descriptor resolved for the target project. </param>
    /// <param name="requestId"> The generated request identifier. </param>
    /// <param name="mode"> The normalized Unity execution mode. </param>
    /// <param name="timeout"> The remaining timeout budget for this internal plan pass. </param>
    /// <param name="failFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="config"> The resolved CLI configuration. </param>
    /// <param name="unityProject"> The resolved Unity project. </param>
    /// <param name="cancellationToken"> The propagated cancellation token. </param>
    /// <returns> A closed issuance result containing either the issued token or the execution failure. </returns>
    private async ValueTask<PlanTokenIssueResult> IssuePlanTokenAsync (
        OperationExecuteDefinition definition,
        UcliOperationDescriptor descriptor,
        Guid requestId,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        ProjectIdentityInfo project,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(unityProject);

        var executionResult = await unityIpcRequestExecutor.ExecuteAsync(
                definition.Command,
                mode,
                timeout,
                config,
                unityProject,
                new UnityRequestPayload.ExecuteOperation(
                    UcliCommandIds.Plan,
                    definition.OperationId,
                    descriptor.Name,
                    definition.Args,
                    failFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(executionResult.FailureInfo!);
            return new PlanTokenIssueResult.Failed(
                OperationExecuteResultFactory.Failure(
                    requestId,
                    [],
                    [
                        failure,
                    ],
                    contractViolations: [],
                    readPostcondition: null,
                    project,
                    postReadSource: null));
        }

        var convertedResponse = ExecuteResponseConverter.Convert(
            executionResult.Response!,
            unityProject);
        if (!TryValidateResponseContract(
                definition,
                descriptor,
                IpcExecuteOperationPhase.Plan,
                convertedResponse,
                out var responseContractError))
        {
            return new PlanTokenIssueResult.Failed(
                OperationExecuteResultFactory.FromExecutionError(
                    requestId,
                    ExecutionError.InternalError(responseContractError, UcliCoreErrorCodes.InternalError),
                    project));
        }

        if (!convertedResponse.IsSuccess)
        {
            return new PlanTokenIssueResult.Failed(
                OperationExecuteResultFactory.Failure(
                    requestId,
                    convertedResponse.OpResults,
                    RequestFailureNormalizer.FromOperationErrors(convertedResponse.Errors),
                    contractViolations: convertedResponse.ContractViolations,
                    readPostcondition: convertedResponse.ReadPostcondition,
                    project,
                    postReadSource: convertedResponse.PostReadSource));
        }

        if (convertedResponse.PlanToken == null)
        {
            return new PlanTokenIssueResult.Failed(
                OperationExecuteResultFactory.Failure(
                    requestId,
                    convertedResponse.OpResults,
                    [
                        ApplicationFailure.ContractViolation(
                            "Execute response payload is invalid. The 'planToken' field is missing.",
                            UcliCoreErrorCodes.InternalError,
                            instancePath: null,
                            startupFailure: null),
                    ],
                    contractViolations: convertedResponse.ContractViolations,
                    readPostcondition: convertedResponse.ReadPostcondition,
                    project,
                    postReadSource: convertedResponse.PostReadSource));
        }

        return new PlanTokenIssueResult.Issued(convertedResponse.PlanToken);
    }

    private static bool TryValidateResponseContract (
        OperationExecuteDefinition definition,
        UcliOperationDescriptor descriptor,
        IpcExecuteOperationPhase executedPass,
        ExecuteResponseConversionResult response,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        var request = new ValidateRequest(
            IpcProtocol.CurrentVersion,
            [
                new ValidateRequestStep(
                    IpcExecuteStepKind.Op,
                    StepIndex: 0,
                    Op: descriptor.Name,
                    Args: definition.Args),
            ],
            AllowPlayMode: false);
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName =
            new Dictionary<string, UcliOperationDescriptor>(StringComparer.Ordinal)
            {
                [descriptor.Name] = descriptor,
            };
        return OperationExecutionResultContractValidator.TryValidate(
            request,
            operationsByName,
            executedPass,
            response,
            out errorMessage);
    }

    private abstract record PlanTokenIssueResult
    {
        private PlanTokenIssueResult ()
        {
        }

        internal sealed record Issued : PlanTokenIssueResult
        {
            internal Issued (string planToken)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(planToken);
                PlanToken = planToken;
            }

            internal string PlanToken { get; }
        }

        internal sealed record Failed : PlanTokenIssueResult
        {
            internal Failed (OperationExecuteResult result)
            {
                Result = result ?? throw new ArgumentNullException(nameof(result));
            }

            internal OperationExecuteResult Result { get; }
        }
    }

}
