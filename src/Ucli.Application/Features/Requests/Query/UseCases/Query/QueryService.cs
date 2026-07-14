using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
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

    private readonly IAssetSearchLookupAccessService assetSearchLookupAccessService;

    private readonly ISceneTreeLiteAccessService sceneTreeLiteAccessService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="QueryService" /> class. </summary>
    public QueryService (
        IProjectContextResolver projectContextResolver,
        IAssetSearchLookupAccessService assetSearchLookupAccessService,
        ISceneTreeLiteAccessService sceneTreeLiteAccessService,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
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
                projectContextResult.Error!);
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
                project: project);
        }

        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, projectContext.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                readIndexModeResult.Error!,
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

            _ => QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                ExecutionError.InvalidArgument(
                    $"Query operation '{input.Operation.OperationName}' is not supported."),
                project: project),
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
            return QueryServiceResultFactory.FromIpcError(
                operation.CommandName,
                requestId,
                new OperationExecutionError(readResult.ErrorCode!.Value, readResult.Message, null),
                ReadIndexInfoFactory.Unity(readResult.Message),
                project);
        }

        var output = readResult.Output!;
        var windowedEntries = BoundedWindowApplicator.Apply(output.Entries, operation.WindowOptions);
        return QueryServiceResultFactory.Success(
            operation.CommandName,
            requestId,
            [
                CreatePlanOperationResult(
                    operation,
                    JsonSerializer.SerializeToElement(CreateAssetsFindResult(windowedEntries), IpcJsonSerializerOptions.Default)),
            ],
            ReadIndexInfoFactory.FromAssetLookupAccess(output.AccessInfo),
            project);
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
            return QueryServiceResultFactory.FromIpcError(
                operation.CommandName,
                requestId,
                new OperationExecutionError(readResult.ErrorCode!.Value, readResult.Message, null),
                ReadIndexInfoFactory.Unity(readResult.Message),
                project);
        }

        var output = readResult.Output!;
        var windowedRoots = SceneTreeWindowProjector.Apply(output.Roots, operation.WindowOptions);
        return QueryServiceResultFactory.Success(
            operation.CommandName,
            requestId,
            [
                CreatePlanOperationResult(
                    operation,
                    JsonSerializer.SerializeToElement(CreateSceneTreeResult(output.ScenePath, windowedRoots, output.SourceState), IpcJsonSerializerOptions.Default)),
            ],
            ReadIndexInfoFactory.FromSceneTreeLiteAccess(output.AccessInfo),
            project);
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
                project);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(
            executionResult.Response!,
            projectContext.UnityProject.ProjectFingerprint);
        var responseProject = convertedResponse.Project ?? project;
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

        var failures = RequestFailureNormalizer.FromOperationErrors(convertedResponse.Errors, "uCLI query failed.");
        return QueryServiceResultFactory.Failure(
            operation.CommandName,
            requestId,
            convertedResponse.OpResults,
            failures,
            RequestFailureNormalizer.ResolveMessage(failures, "uCLI query failed."),
            readIndex,
            responseProject,
            convertedResponse.ContractViolations);
    }

    private static OperationExecutionOperationResult CreatePlanOperationResult (
        QueryOperationRequest operation,
        JsonElement result)
    {
        return OperationExecutionModelMapper.CreatePlanResult(
            opId: operation.OperationId,
            op: operation.OperationName,
            applied: false,
            changed: false,
            touched: [],
            result: result);
    }

    private static AssetsFindResult CreateAssetsFindResult (BoundedWindowResult<IndexAssetSearchEntryJsonContract> windowedEntries)
    {
        var matches = new AssetsFindMatch[windowedEntries.Items.Count];
        for (var i = 0; i < windowedEntries.Items.Count; i++)
        {
            var entry = windowedEntries.Items[i];
            var assetGuid = entry.AssetGuid
                ?? throw new InvalidOperationException("Validated asset-search entry must specify assetGuid.");
            matches[i] = new AssetsFindMatch(
                assetPath: new UnityAssetPath(entry.AssetPath!),
                assetGuid: assetGuid.Length == 0 ? null : new UnityAssetGuid(assetGuid),
                name: entry.Name!,
                typeId: new UnityTypeId(entry.TypeId!));
        }

        return new AssetsFindResult(matches, windowedEntries.Window);
    }

    private static SceneTreeResult CreateSceneTreeResult (
        string scenePath,
        BoundedWindowResult<IndexSceneTreeLiteNodeJsonContract> windowedRoots,
        SceneTreeSourceState sourceState)
    {
        return new SceneTreeResult(
            path: new SceneAssetPath(scenePath),
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
