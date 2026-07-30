using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Implements the <c>resolve</c> workflow across read-index and Unity IPC fallback paths. </summary>
internal sealed class ResolveService : IResolveService
{
    private static readonly IpcExecuteStepId ResolveOperationId = new("resolve");

    private readonly IProjectContextResolver projectContextResolver;

    private readonly ISceneTreeLiteAccessService sceneTreeLiteAccessService;

    private readonly IOperationCatalog operationCatalog;

    private readonly IReadIndexValidationCatalogResolver readIndexValidationCatalogResolver;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="ResolveService" /> class. </summary>
    public ResolveService (
        IProjectContextResolver projectContextResolver,
        ISceneTreeLiteAccessService sceneTreeLiteAccessService,
        IOperationCatalog operationCatalog,
        IReadIndexValidationCatalogResolver readIndexValidationCatalogResolver,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.sceneTreeLiteAccessService = sceneTreeLiteAccessService ?? throw new ArgumentNullException(nameof(sceneTreeLiteAccessService));
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
        this.readIndexValidationCatalogResolver = readIndexValidationCatalogResolver ?? throw new ArgumentNullException(nameof(readIndexValidationCatalogResolver));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<ResolveServiceResult> ExecuteAsync (
        Guid requestId,
        ResolveCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Selector);
        var projectContextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return ResolveServiceResultFactory.FromExecutionError(
                requestId,
                projectContextResult.Error!,
                ReadIndexInfoFactory.Unity(fallbackReason: null),
                project: null);
        }

        var projectContext = projectContextResult.Context!;
        var project = ProjectIdentityInfo.From(projectContext.UnityProject);
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Resolve,
            projectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return ResolveServiceResultFactory.FromExecutionError(
                requestId,
                timeoutResolutionResult.Error!,
                ReadIndexInfoFactory.Unity(fallbackReason: null),
                project);
        }

        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, projectContext.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            return ResolveServiceResultFactory.FromExecutionError(
                requestId,
                readIndexModeResult.Error!,
                ReadIndexInfoFactory.Unity(fallbackReason: null),
                project);
        }

        var executionMode = input.Mode ?? UnityExecutionMode.Auto;
        var timeout = timeoutResolutionResult.Timeout!.Value;
        var readIndexMode = readIndexModeResult.Mode!.Value;

        if (input.Selector is ResolveSceneHierarchySelectorInput sceneHierarchySelector && readIndexMode != ReadIndexMode.Disabled)
        {
            var indexResult = await TryResolveFromSceneTreeLiteIndexAsync(
                    requestId,
                    input,
                    sceneHierarchySelector,
                    projectContext,
                    project,
                    executionMode,
                    timeout,
                    readIndexMode,
                    cancellationToken)
                .ConfigureAwait(false);
            if (indexResult.CompletedResult != null)
            {
                return indexResult.CompletedResult;
            }

            return await ExecuteResolveInUnityAsync(
                    requestId,
                    input,
                    projectContext,
                    project,
                    executionMode,
                    timeout,
                    indexResult.FallbackReason,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var fallbackReason = ResolveFallbackReason(input.Selector, readIndexMode);
        return await ExecuteResolveInUnityAsync(
                requestId,
                input,
                projectContext,
                project,
                executionMode,
                timeout,
                fallbackReason,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(ResolveServiceResult? CompletedResult, string FallbackReason)> TryResolveFromSceneTreeLiteIndexAsync (
        Guid requestId,
        ResolveCommandInput input,
        ResolveSceneHierarchySelectorInput selector,
        ProjectContext projectContext,
        ProjectIdentityInfo project,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken)
    {
        var readResult = await sceneTreeLiteAccessService.ReadAsync(
                projectContext.UnityProject,
                projectContext.Config,
                UcliCommandIds.Resolve,
                executionMode,
                timeout,
                readIndexMode,
                selector.Scene,
                depth: null,
                failFast: input.FailFast,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            if (readResult.ErrorCode == UcliCoreErrorCodes.InvalidArgument)
            {
                return (
                    ResolveServiceResultFactory.Failure(
                        requestId,
                        opResults: [],
                        errors:
                        [
                            ApplicationFailure.FromCode(
                                readResult.ErrorCode,
                                readResult.Message),
                        ],
                        ReadIndexInfoFactory.Unity(readResult.Message),
                        project,
                        contractViolations: []),
                    readResult.Message);
            }

            return (null, readResult.Message);
        }

        var output = readResult.Output!;
        if (output.AccessInfo.Source != SceneTreeLiteSource.Index)
        {
            return (null, output.AccessInfo.FallbackReason ?? "scene-tree-lite readIndex was not used.");
        }

        var resolveResult = SceneTreeLiteHierarchyPathResolver.Resolve(output.Roots, selector.HierarchyPath);
        if (!resolveResult.IsSuccess)
        {
            return (null, resolveResult.ErrorMessage!);
        }

        var readIndex = ReadIndexInfoFactory.FromSceneTreeLiteAccess(output.AccessInfo);
        UcliOperationDescriptor operationDescriptor;
        try
        {
            operationDescriptor = await readIndexValidationCatalogResolver.ResolveOperationAsync(
                    projectContext.UnityProject,
                    readIndexMode,
                    output.AccessInfo.GeneratedAtUtc,
                    UcliPrimitiveOperationNames.Resolve,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return (
                CreateOperationMetadataFailure(
                    requestId,
                    exception,
                    readIndex,
                    project),
                string.Empty);
        }

        return (
            ResolveServiceResultFactory.Success(
                requestId,
                [
                    CreateResolveOperationResult(
                        resolveResult.GlobalObjectId!,
                        operationDescriptor.DescriptorDigest),
                ],
                readIndex,
                project,
                contractViolations: []),
            string.Empty);
    }

    private async ValueTask<ResolveServiceResult> ExecuteResolveInUnityAsync (
        Guid requestId,
        ResolveCommandInput input,
        ProjectContext projectContext,
        ProjectIdentityInfo project,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var readIndex = ReadIndexInfoFactory.Unity(fallbackReason);
        UcliOperationDescriptor operationDescriptor;
        try
        {
            operationDescriptor = await ResolveLiveOperationDescriptorAsync(
                    projectContext,
                    executionMode,
                    timeout,
                    input.FailFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return CreateOperationMetadataFailure(
                requestId,
                exception,
                readIndex,
                project);
        }

        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.Resolve,
                executionMode,
                timeout,
                projectContext.Config,
                projectContext.UnityProject,
                CreateExecuteRequestPayload(input.Selector, input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(executionResult.FailureInfo!);
            return ResolveServiceResultFactory.Failure(
                requestId,
                [],
                [
                    failure,
                ],
                readIndex,
                project,
                contractViolations: []);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(
            executionResult.Response!,
            projectContext.UnityProject);
        var responseProject = convertedResponse.Project ?? project;
        if (!OperationExecutionResultContractValidator.TryValidateDirectOperation(
                operationDescriptor,
                IpcExecuteOperationPhase.Plan,
                convertedResponse,
                out var contractError))
        {
            return ResolveServiceResultFactory.FromExecutionError(
                requestId,
                ExecutionError.InternalError(contractError, UcliCoreErrorCodes.InternalError),
                readIndex,
                responseProject);
        }

        if (convertedResponse.IsSuccess)
        {
            return ResolveServiceResultFactory.Success(
                requestId,
                convertedResponse.OpResults,
                readIndex,
                responseProject,
                convertedResponse.ContractViolations);
        }

        return ResolveServiceResultFactory.Failure(
            requestId,
            convertedResponse.OpResults,
            RequestFailureNormalizer.FromOperationErrors(convertedResponse.Errors),
            readIndex,
            responseProject,
            convertedResponse.ContractViolations);
    }

    private async ValueTask<UcliOperationDescriptor> ResolveLiveOperationDescriptorAsync (
        ProjectContext projectContext,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var operations = await operationCatalog.GetAllAsync(
                projectContext.UnityProject,
                projectContext.Config,
                executionMode,
                timeout,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        return operations.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Name,
                    UcliPrimitiveOperationNames.Resolve,
                    StringComparison.Ordinal))
            ?? throw OperationCatalogLoadException.Create(
                ApplicationFailure.InvalidInput(
                    $"Operation '{UcliPrimitiveOperationNames.Resolve}' is not registered.",
                    ValidationErrorCodes.OperationNotFound),
                "Operation catalog is incomplete.");
    }

    private static ResolveServiceResult CreateOperationMetadataFailure (
        Guid requestId,
        InvalidOperationException exception,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project)
    {
        if (exception is OperationCatalogLoadException catalogException)
        {
            var failure = catalogException.CreatePrefixedFailure("uCLI resolve could not load operation metadata.");
            return ResolveServiceResultFactory.Failure(
                requestId,
                opResults: [],
                errors: [failure],
                readIndex,
                project,
                contractViolations: []);
        }

        return ResolveServiceResultFactory.FromExecutionError(
            requestId,
            ExecutionError.InternalError(
                $"uCLI resolve could not load operation metadata. {exception.Message}"),
            readIndex,
            project);
    }

    private static UnityRequestPayload CreateExecuteRequestPayload (
        ResolveSelectorInput selector,
        bool failFast)
    {
        return new UnityRequestPayload.ExecuteOperation(
            UcliCommandIds.Resolve,
            ResolveOperationId,
            UcliPrimitiveOperationNames.Resolve,
            ResolveSelectorOperationArgsFactory.Create(selector),
            failFast);
    }

    private static OperationExecutionOperationResult CreateResolveOperationResult (
        UnityGlobalObjectId globalObjectId,
        Sha256Digest operationDescriptorDigest)
    {
        return OperationExecutionModelMapper.CreatePlanResult(
            op: UcliPrimitiveOperationNames.Resolve,
            applied: false,
            changed: false,
            touched: [],
            operationDescriptorDigest: operationDescriptorDigest,
            result: JsonSerializer.SerializeToElement(new ResolveOperationResult(globalObjectId), IpcJsonSerializerOptions.Default));
    }

    private static string ResolveFallbackReason (
        ResolveSelectorInput selector,
        ReadIndexMode readIndexMode)
    {
        if (selector is ResolveSceneHierarchySelectorInput && readIndexMode == ReadIndexMode.Disabled)
        {
            return "readIndex disabled by mode.";
        }

        return "selector requires live Unity resolution.";
    }

    private sealed record ResolveOperationResult (UnityGlobalObjectId GlobalObjectId);

}
