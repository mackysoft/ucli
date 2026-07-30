using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Implements typed query workflows across read-index and Unity IPC paths. </summary>
internal sealed class QueryService : IQueryService
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IOperationCatalog operationCatalog;

    private readonly IReadIndexValidationCatalogResolver readIndexValidationCatalogResolver;

    private readonly IAssetSearchLookupAccessService assetSearchLookupAccessService;

    private readonly ISceneTreeLiteAccessService sceneTreeLiteAccessService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="QueryService" /> class. </summary>
    /// <param name="projectContextResolver"> The project-context resolver. </param>
    /// <param name="operationCatalog"> The authoritative operation catalog. </param>
    /// <param name="readIndexValidationCatalogResolver"> The persisted operation-catalog resolver. </param>
    /// <param name="assetSearchLookupAccessService"> The asset-search read-index access service. </param>
    /// <param name="sceneTreeLiteAccessService"> The scene-tree read-index access service. </param>
    /// <param name="unityRequestExecutor"> The Unity request executor. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public QueryService (
        IProjectContextResolver projectContextResolver,
        IOperationCatalog operationCatalog,
        IReadIndexValidationCatalogResolver readIndexValidationCatalogResolver,
        IAssetSearchLookupAccessService assetSearchLookupAccessService,
        ISceneTreeLiteAccessService sceneTreeLiteAccessService,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
        this.readIndexValidationCatalogResolver = readIndexValidationCatalogResolver ?? throw new ArgumentNullException(nameof(readIndexValidationCatalogResolver));
        this.assetSearchLookupAccessService = assetSearchLookupAccessService ?? throw new ArgumentNullException(nameof(assetSearchLookupAccessService));
        this.sceneTreeLiteAccessService = sceneTreeLiteAccessService ?? throw new ArgumentNullException(nameof(sceneTreeLiteAccessService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<QueryServiceResult> ExecuteAsync (
        Guid requestId,
        QueryCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Operation);
        var projectContextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                projectContextResult.Error!,
                ReadIndexInfoFactory.Unity(fallbackReason: null),
                project: null);
        }

        var projectContext = projectContextResult.Context!;
        var project = ProjectIdentityInfo.From(projectContext.UnityProject);
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Query,
            projectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                timeoutResolutionResult.Error!,
                ReadIndexInfoFactory.Unity(fallbackReason: null),
                project: project);
        }

        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, projectContext.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                readIndexModeResult.Error!,
                ReadIndexInfoFactory.Unity(fallbackReason: null),
                project: project);
        }

        var executionMode = input.Mode ?? UnityExecutionMode.Auto;
        var timeout = timeoutResolutionResult.Timeout!.Value;
        var readIndexMode = readIndexModeResult.Mode!.Value;

        return input.Operation switch
        {
            QueryAssetsFindOperationRequest assetsFind => await ExecuteAssetsFindAsync(
                    requestId,
                    assetsFind,
                    projectContext,
                    project,
                    executionMode,
                    timeout,
                    readIndexMode,
                    input.FailFast,
                    cancellationToken)
                .ConfigureAwait(false),

            QuerySceneTreeOperationRequest sceneTree => await ExecuteSceneTreeAsync(
                    requestId,
                    input,
                    sceneTree,
                    projectContext,
                    project,
                    executionMode,
                    timeout,
                    readIndexMode,
                    cancellationToken)
                .ConfigureAwait(false),

            QueryUnityOperationRequest unityOperation => await ExecuteInUnityAsync(
                    requestId,
                    input,
                    unityOperation,
                    projectContext,
                    project,
                    executionMode,
                    timeout,
                    readIndexMode,
                    cancellationToken)
                .ConfigureAwait(false),

            _ => throw new InvalidOperationException(
                $"Query operation request type '{input.Operation.GetType().FullName}' has no execution path."),
        };
    }

    private async ValueTask<QueryServiceResult> ExecuteAssetsFindAsync (
        Guid requestId,
        QueryAssetsFindOperationRequest operation,
        ProjectContext projectContext,
        ProjectIdentityInfo project,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var readResult = await assetSearchLookupAccessService.SearchAsync(
                projectContext.UnityProject,
                projectContext.Config,
                executionMode,
                timeout,
                readIndexMode,
                operation.Query,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return QueryServiceResultFactory.Failure(
                operation.CommandName,
                requestId,
                opResults: [],
                errors:
                [
                    ApplicationFailure.FromCode(
                        readResult.ErrorCode,
                        readResult.Message),
                ],
                message: readResult.Message,
                ReadIndexInfoFactory.Unity(readResult.Message),
                project,
                contractViolations: []);
        }

        var output = readResult.Output!;
        var readIndex = ReadIndexInfoFactory.FromAssetLookupAccess(output.AccessInfo);
        UcliOperationDescriptor operationDescriptor;
        try
        {
            operationDescriptor = output.AccessInfo.Source == AssetLookupSource.Index
                ? await readIndexValidationCatalogResolver.ResolveOperationAsync(
                        projectContext.UnityProject,
                        readIndexMode,
                        output.AccessInfo.GeneratedAtUtc,
                        operation.OperationName,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await ResolveLiveOperationDescriptorAsync(
                        operation.OperationName,
                        projectContext,
                        executionMode,
                        timeout,
                        failFast,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return CreateOperationMetadataFailure(
                operation.CommandName,
                requestId,
                exception,
                readIndex,
                project);
        }

        var windowedEntries = BoundedWindowApplicator.Apply(output.Entries, operation.WindowOptions);
        return QueryServiceResultFactory.Success(
            operation.CommandName,
            requestId,
            [
                CreatePlanOperationResult(
                    operation,
                    operationDescriptor,
                    JsonSerializer.SerializeToElement(CreateAssetsFindResult(windowedEntries), IpcJsonSerializerOptions.Default)),
            ],
            readIndex,
            project,
            contractViolations: []);
    }

    private async ValueTask<QueryServiceResult> ExecuteSceneTreeAsync (
        Guid requestId,
        QueryCommandInput input,
        QuerySceneTreeOperationRequest operation,
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
                UcliCommandIds.Query,
                executionMode,
                timeout,
                readIndexMode,
                operation.ScenePath,
                operation.Depth,
                input.FailFast,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return QueryServiceResultFactory.Failure(
                operation.CommandName,
                requestId,
                opResults: [],
                errors:
                [
                    ApplicationFailure.FromCode(
                        readResult.ErrorCode,
                        readResult.Message),
                ],
                message: readResult.Message,
                ReadIndexInfoFactory.Unity(readResult.Message),
                project,
                contractViolations: []);
        }

        var output = readResult.Output!;
        var readIndex = ReadIndexInfoFactory.FromSceneTreeLiteAccess(output.AccessInfo);
        UcliOperationDescriptor operationDescriptor;
        try
        {
            operationDescriptor = output.AccessInfo.Source == SceneTreeLiteSource.Index
                ? await readIndexValidationCatalogResolver.ResolveOperationAsync(
                        projectContext.UnityProject,
                        readIndexMode,
                        output.AccessInfo.GeneratedAtUtc,
                        operation.OperationName,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await ResolveLiveOperationDescriptorAsync(
                        operation.OperationName,
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
                operation.CommandName,
                requestId,
                exception,
                readIndex,
                project);
        }

        var windowedRoots = SceneTreeWindowProjector.Apply(
            ReadIndexJsonContractMapper.ToJsonContracts(output.Roots),
            operation.WindowOptions);
        return QueryServiceResultFactory.Success(
            operation.CommandName,
            requestId,
            [
                CreatePlanOperationResult(
                    operation,
                    operationDescriptor,
                    JsonSerializer.SerializeToElement(CreateSceneTreeResult(output.ScenePath, windowedRoots, output.SourceState), IpcJsonSerializerOptions.Default)),
            ],
            readIndex,
            project,
            contractViolations: []);
    }

    private async ValueTask<QueryServiceResult> ExecuteInUnityAsync (
        Guid requestId,
        QueryCommandInput input,
        QueryUnityOperationRequest operation,
        ProjectContext projectContext,
        ProjectIdentityInfo project,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken)
    {
        var readIndex = ReadIndexInfoFactory.Unity(ResolveUnityOnlyFallbackReason(readIndexMode));
        UcliOperationDescriptor operationDescriptor;
        try
        {
            operationDescriptor = await ResolveLiveOperationDescriptorAsync(
                    operation.OperationName,
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
                operation.CommandName,
                requestId,
                exception,
                readIndex,
                project);
        }

        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.Query,
                executionMode,
                timeout,
                projectContext.Config,
                projectContext.UnityProject,
                new UnityRequestPayload.ExecuteOperation(
                    UcliCommandIds.Query,
                    operation.OperationId,
                    operation.OperationName,
                    operation.Args,
                    input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(executionResult.FailureInfo!);
            return QueryServiceResultFactory.Failure(
                operation.CommandName,
                requestId,
                [],
                [
                    failure,
                ],
                failure.Message,
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
                out var responseContractError))
        {
            return QueryServiceResultFactory.FromExecutionError(
                operation.CommandName,
                requestId,
                ExecutionError.InternalError(responseContractError, UcliCoreErrorCodes.InternalError),
                readIndex,
                responseProject);
        }

        if (convertedResponse.IsSuccess)
        {
            return QueryServiceResultFactory.Success(
                operation.CommandName,
                requestId,
                convertedResponse.OpResults,
                readIndex,
                responseProject,
                convertedResponse.ContractViolations);
        }

        var failures = RequestFailureNormalizer.FromOperationErrors(convertedResponse.Errors);
        return QueryServiceResultFactory.Failure(
            operation.CommandName,
            requestId,
            convertedResponse.OpResults,
            failures,
            failures[0].Message,
            readIndex,
            responseProject,
            convertedResponse.ContractViolations);
    }

    private async ValueTask<UcliOperationDescriptor> ResolveLiveOperationDescriptorAsync (
        string operationName,
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
                string.Equals(candidate.Name, operationName, StringComparison.Ordinal))
            ?? throw OperationCatalogLoadException.Create(
                ApplicationFailure.InvalidInput(
                    $"Operation '{operationName}' is not registered.",
                    ValidationErrorCodes.OperationNotFound),
                "Operation catalog is incomplete.");
    }

    private static QueryServiceResult CreateOperationMetadataFailure (
        string commandName,
        Guid requestId,
        InvalidOperationException exception,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project)
    {
        if (exception is OperationCatalogLoadException catalogException)
        {
            var failure = catalogException.CreatePrefixedFailure("Query could not load operation metadata.");
            return QueryServiceResultFactory.Failure(
                commandName,
                requestId,
                opResults: [],
                errors: [failure],
                message: failure.Message,
                readIndex,
                project,
                contractViolations: []);
        }

        return QueryServiceResultFactory.FromExecutionError(
            commandName,
            requestId,
            ExecutionError.InternalError(
                $"Query could not load operation metadata. {exception.Message}"),
            readIndex,
            project);
    }

    private static OperationExecutionOperationResult CreatePlanOperationResult (
        QueryOperationRequest operation,
        UcliOperationDescriptor operationDescriptor,
        JsonElement result)
    {
        return OperationExecutionModelMapper.CreatePlanResult(
            op: operation.OperationName,
            applied: false,
            changed: false,
            touched: [],
            operationDescriptorDigest: operationDescriptor.DescriptorDigest,
            result: result);
    }

    private static AssetsFindResult CreateAssetsFindResult (BoundedWindowResult<AssetSearchLookupEntry> windowedEntries)
    {
        var matches = new AssetsFindMatch[windowedEntries.Items.Count];
        for (var i = 0; i < windowedEntries.Items.Count; i++)
        {
            var entry = windowedEntries.Items[i];
            matches[i] = new AssetsFindMatch(
                assetPath: entry.AssetPath,
                assetGuid: entry.AssetGuid,
                name: entry.Name,
                typeId: entry.TypeId);
        }

        return new AssetsFindResult(matches, windowedEntries.Window);
    }

    private static SceneTreeResult CreateSceneTreeResult (
        UnityScenePath scenePath,
        BoundedWindowResult<IndexSceneTreeLiteNodeJsonContract> windowedRoots,
        SceneTreeSourceState sourceState)
    {
        return new SceneTreeResult(
            path: scenePath,
            roots: windowedRoots.Items,
            sourceState: sourceState,
            window: windowedRoots.Window);
    }

    private static string ResolveUnityOnlyFallbackReason (ReadIndexMode readIndexMode)
    {
        return readIndexMode == ReadIndexMode.Disabled
            ? "readIndex disabled by mode."
            : "query operation is not backed by readIndex.";
    }

}
