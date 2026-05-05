using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
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
    public async ValueTask<QueryServiceResult> Execute (
        QueryCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Operation);

        var requestId = Guid.NewGuid().ToString("D");
        var projectContextResult = await projectContextResolver.Resolve(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                projectContextResult.Error!);
        }

        var projectContext = projectContextResult.Context!;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Query,
            projectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                timeoutResolutionResult.Error!);
        }

        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, projectContext.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                readIndexModeResult.Error!);
        }

        var executionMode = input.Mode ?? UnityExecutionMode.Auto;
        var timeout = timeoutResolutionResult.Timeout!.Value;
        var readIndexMode = readIndexModeResult.Mode!.Value;

        return input.Operation switch
        {
            QueryAssetsFindOperationRequest assetsFind => await ExecuteAssetsFind(
                    requestId,
                    assetsFind,
                    projectContext,
                    executionMode,
                    timeout,
                    readIndexMode,
                    input.FailFast,
                    cancellationToken)
                .ConfigureAwait(false),

            QuerySceneTreeOperationRequest sceneTree => await ExecuteSceneTree(
                    requestId,
                    input,
                    sceneTree,
                    projectContext,
                    executionMode,
                    timeout,
                    readIndexMode,
                    cancellationToken)
                .ConfigureAwait(false),

            QueryUnityOperationRequest unityOperation => await ExecuteInUnity(
                    requestId,
                    input,
                    unityOperation,
                    projectContext,
                    executionMode,
                    timeout,
                    readIndexMode,
                    cancellationToken)
                .ConfigureAwait(false),

            _ => QueryServiceResultFactory.FromExecutionError(
                input.Operation.CommandName,
                requestId,
                ExecutionError.InvalidArgument(
                    $"Query operation '{input.Operation.OperationName}' is not supported.")),
        };
    }

    private async ValueTask<QueryServiceResult> ExecuteAssetsFind (
        string requestId,
        QueryAssetsFindOperationRequest operation,
        ProjectContext projectContext,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var query = new AssetSearchLookupQuery(
            TypeId: operation.Filter.TypeId,
            PathPrefix: operation.Filter.PathPrefix,
            NameContains: operation.Filter.NameContains);
        var readResult = await assetSearchLookupAccessService.Search(
                projectContext.UnityProject,
                projectContext.Config,
                executionMode,
                timeout,
                readIndexMode,
                query,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return QueryServiceResultFactory.FromIpcError(
                operation.CommandName,
                requestId,
                new IpcError(ResolveErrorCode(readResult.ErrorCode), readResult.Message, null),
                QueryReadIndexInfoFactory.Unity(readResult.Message));
        }

        var output = readResult.Output!;
        var windowedEntries = QueryWindowApplicator.Apply(output.Entries, operation.WindowOptions);
        return QueryServiceResultFactory.Success(
            operation.CommandName,
            requestId,
            [
                CreatePlanOperationResult(
                    operation,
                    IpcPayloadCodec.SerializeToElement(CreateAssetsFindResult(windowedEntries))),
            ],
            QueryReadIndexInfoFactory.FromAssetLookupAccess(output.AccessInfo));
    }

    private async ValueTask<QueryServiceResult> ExecuteSceneTree (
        string requestId,
        QueryCommandInput input,
        QuerySceneTreeOperationRequest operation,
        ProjectContext projectContext,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken)
    {
        var readResult = await sceneTreeLiteAccessService.Read(
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
                new IpcError(ResolveErrorCode(readResult.ErrorCode), readResult.Message, null),
                QueryReadIndexInfoFactory.Unity(readResult.Message));
        }

        var output = readResult.Output!;
        var windowedRoots = QueryWindowApplicator.Apply(output.Roots, operation.WindowOptions);
        return QueryServiceResultFactory.Success(
            operation.CommandName,
            requestId,
            [
                CreatePlanOperationResult(
                    operation,
                    IpcPayloadCodec.SerializeToElement(CreateSceneTreeResult(output.ScenePath, windowedRoots))),
            ],
            QueryReadIndexInfoFactory.FromSceneTreeLiteAccess(output.AccessInfo));
    }

    private async ValueTask<QueryServiceResult> ExecuteInUnity (
        string requestId,
        QueryCommandInput input,
        QueryUnityOperationRequest operation,
        ProjectContext projectContext,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken)
    {
        var readIndex = QueryReadIndexInfoFactory.Unity(ResolveUnityOnlyFallbackReason(readIndexMode));
        var executionResult = await unityRequestExecutor.Execute(
                UcliCommandIds.Query,
                executionMode,
                timeout,
                projectContext.Config,
                projectContext.UnityProject,
                IpcMethodNames.Execute,
                ExecuteRequestPayloadFactory.CreateSingleOperation(
                    UcliCommandIds.Query,
                    requestId,
                    operation.OperationId,
                    operation.OperationName,
                    operation.Args,
                    input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var errorCode = ResolveErrorCode(executionResult.ErrorCode);
            return QueryServiceResultFactory.Create(
                operation.CommandName,
                requestId,
                [],
                [
                    new IpcError(errorCode, executionResult.Message, null),
                ],
                ExecuteResponseConverter.ResolveOutcome(errorCode),
                executionResult.Message,
                readIndex);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(executionResult.Response!);
        return QueryServiceResultFactory.Create(
            operation.CommandName,
            requestId,
            convertedResponse.OpResults,
            convertedResponse.Errors,
            convertedResponse.Outcome,
            convertedResponse.Errors.Count == 0
                ? "uCLI query completed."
                : ResolveFailureMessage(convertedResponse.Errors),
            readIndex);
    }

    private static IpcExecuteOperationResult CreatePlanOperationResult (
        QueryOperationRequest operation,
        JsonElement result)
    {
        return IpcExecuteOperationResultFactory.CreatePlanResult(
            opId: operation.OperationId,
            op: operation.OperationName,
            applied: false,
            changed: false,
            touched: [],
            result: result);
    }

    private static AssetsFindResult CreateAssetsFindResult (QueryWindowResult<IndexAssetSearchEntryJsonContract> windowedEntries)
    {
        var matches = new AssetsFindMatch[windowedEntries.Items.Count];
        for (var i = 0; i < windowedEntries.Items.Count; i++)
        {
            var entry = windowedEntries.Items[i];
            matches[i] = new AssetsFindMatch(
                AssetPath: entry.AssetPath,
                AssetGuid: entry.AssetGuid,
                Name: entry.Name,
                TypeId: entry.TypeId);
        }

        return new AssetsFindResult(matches, windowedEntries.Window);
    }

    private static SceneTreeResult CreateSceneTreeResult (
        string scenePath,
        QueryWindowResult<IndexSceneTreeLiteNodeJsonContract> windowedRoots)
    {
        return new SceneTreeResult(
            Path: scenePath,
            Roots: windowedRoots.Items,
            Window: windowedRoots.Window);
    }

    private static string ResolveUnityOnlyFallbackReason (ReadIndexMode readIndexMode)
    {
        return readIndexMode == ReadIndexMode.Disabled
            ? "readIndex disabled by mode."
            : "query operation is not backed by readIndex.";
    }

    private static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
    }

    private static string ResolveFailureMessage (IReadOnlyList<IpcError> errors)
    {
        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }
        }

        return "uCLI query failed.";
    }

    private sealed record AssetsFindResult (
        IReadOnlyList<AssetsFindMatch> Matches,
        QueryWindowInfo Window);

    private sealed record AssetsFindMatch (
        string? AssetPath,
        string? AssetGuid,
        string? Name,
        string? TypeId);

    private sealed record SceneTreeResult (
        string Path,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots,
        QueryWindowInfo Window);
}
